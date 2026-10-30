// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Shinra CC

using System.Runtime.Versioning;
using ShinraShot.Core.Input;
using ShinraShot.Core.Settings;

namespace ShinraShot.Core.Platform.Linux;

/// <summary>
/// Linux (X11) global hotkeys via <c>XGrabKey</c> on the root window. An X grab delivers its
/// key events only to the grabbing connection, so the grab and the event loop share one
/// <c>Display</c> on a single owned thread. Each chord is grabbed for the lock-key permutations
/// (CapsLock / NumLock) so it fires regardless of lock state.
///
/// Unlike the Windows backend this does not detect grab conflicts: Xlib only reports a clash
/// (BadAccess) through the process-global error handler, which Avalonia's own X connection also
/// relies on, so we leave it alone. A chord already held by the window manager is reported as
/// registered but simply never fires. Also X11-only — on Wayland the grab reaches XWayland and
/// will not see native-client key presses.
/// </summary>
[SupportedOSPlatform("linux")]
public sealed class LinuxGlobalHotkeys : IGlobalHotkeys
{
    private const int PollMs = 20;

    // Grab each chord with these extra lock bits so CapsLock / NumLock don't suppress it.
    private static readonly uint[] LockCombos =
    {
        0,
        X11Native.LockMask,
        X11Native.Mod2Mask,
        X11Native.LockMask | X11Native.Mod2Mask,
    };

    private readonly record struct Grab(int Keycode, uint Mask, string Action);

    private readonly Thread _thread;
    private readonly ManualResetEventSlim _ready = new(false);
    private readonly ManualResetEventSlim _applied = new(false);
    private readonly object _gate = new();
    private readonly List<Grab> _active = new(); // touched only on the X thread

    private volatile bool _running = true;
    private nint _display;
    private nint _root;
    private bool _openFailed;

    private List<HotkeyBinding>? _pending;
    private IReadOnlyList<HotkeyRegistration>? _results;

    public event Action<string>? Pressed;

    public LinuxGlobalHotkeys()
    {
        _thread = new Thread(EventLoop)
        {
            IsBackground = true,
            Name = "ShinraShot X11 hotkeys",
        };
        _thread.Start();
    }

    public IReadOnlyList<HotkeyRegistration> Apply(IReadOnlyCollection<HotkeyBinding> bindings)
    {
        if (!_ready.Wait(TimeSpan.FromSeconds(2)) || _openFailed)
            return Fail(bindings, "No X11 display available.");

        _applied.Reset();
        lock (_gate)
            _pending = new List<HotkeyBinding>(bindings);

        if (!_applied.Wait(TimeSpan.FromSeconds(2)))
            return Fail(bindings, "Timed out registering hotkeys.");

        return _results ?? Array.Empty<HotkeyRegistration>();
    }

    public void Dispose()
    {
        _running = false;
        if (_thread.IsAlive)
            _thread.Join(TimeSpan.FromSeconds(2));
        _ready.Dispose();
        _applied.Dispose();
    }

    private void EventLoop()
    {
        _display = X11Native.XOpenDisplay(null);
        if (_display == 0)
        {
            _openFailed = true;
            _ready.Set();
            return;
        }

        int screen = X11Native.XDefaultScreen(_display);
        _root = X11Native.XRootWindow(_display, screen);
        _ready.Set();

        var ev = default(X11Native.XEvent);
        while (_running)
        {
            List<HotkeyBinding>? request = null;
            lock (_gate)
            {
                if (_pending is not null)
                {
                    request = _pending;
                    _pending = null;
                }
            }

            if (request is not null)
            {
                _results = Regrab(request);
                _applied.Set();
            }

            while (_running && X11Native.XPending(_display) > 0)
            {
                X11Native.XNextEvent(_display, ref ev);
                if (ev.type == X11Native.KeyPress)
                    DispatchKeyPress(ev);
            }

            Thread.Sleep(PollMs);
        }

        UngrabAll();
        X11Native.XCloseDisplay(_display);
        _display = 0;
    }

    private void DispatchKeyPress(in X11Native.XEvent ev)
    {
        uint cleaned = ev.state & ~(X11Native.LockMask | X11Native.Mod2Mask);
        foreach (var g in _active)
        {
            if (ev.keycode == (uint)g.Keycode && cleaned == g.Mask)
            {
                Pressed?.Invoke(g.Action);
                return;
            }
        }
    }

    private IReadOnlyList<HotkeyRegistration> Regrab(List<HotkeyBinding> bindings)
    {
        UngrabAll();

        var results = new List<HotkeyRegistration>(bindings.Count);
        foreach (var b in bindings)
        {
            if (!b.Hotkey.IsBound)
            {
                results.Add(new HotkeyRegistration(b.Id, b.Hotkey, false, "No key assigned."));
                continue;
            }

            if (!TryMapKeysym(b.Hotkey.Key, out nuint keysym))
            {
                results.Add(new HotkeyRegistration(b.Id, b.Hotkey, false, $"Unsupported key '{b.Hotkey.Key}'."));
                continue;
            }

            byte keycode = X11Native.XKeysymToKeycode(_display, keysym);
            if (keycode == 0)
            {
                results.Add(new HotkeyRegistration(b.Id, b.Hotkey, false, $"Key '{b.Hotkey.Key}' is not on the current layout."));
                continue;
            }

            uint mask = ToXModifiers(b.Hotkey.Modifiers);
            foreach (uint lk in LockCombos)
                X11Native.XGrabKey(_display, keycode, mask | lk, _root, false, X11Native.GrabModeAsync, X11Native.GrabModeAsync);

            _active.Add(new Grab(keycode, mask, b.Id));
            results.Add(new HotkeyRegistration(b.Id, b.Hotkey, true, null));
        }

        X11Native.XSync(_display, false);
        return results;
    }

    private void UngrabAll()
    {
        if (_display == 0)
        {
            _active.Clear();
            return;
        }

        foreach (var g in _active)
            foreach (uint lk in LockCombos)
                X11Native.XUngrabKey(_display, g.Keycode, g.Mask | lk, _root);

        _active.Clear();
        X11Native.XSync(_display, false);
    }

    private static uint ToXModifiers(HotkeyModifiers modifiers)
    {
        uint result = 0;
        if (modifiers.HasFlag(HotkeyModifiers.Control)) result |= X11Native.ControlMask;
        if (modifiers.HasFlag(HotkeyModifiers.Alt)) result |= X11Native.Mod1Mask;
        if (modifiers.HasFlag(HotkeyModifiers.Shift)) result |= X11Native.ShiftMask;
        if (modifiers.HasFlag(HotkeyModifiers.Meta)) result |= X11Native.Mod4Mask;
        return result;
    }

    /// <summary>Map a canonical key name to an X11 keysym value.</summary>
    private static bool TryMapKeysym(string key, out nuint keysym)
    {
        keysym = 0;
        if (string.IsNullOrEmpty(key))
            return false;

        if (key.Length == 1)
        {
            char c = char.ToUpperInvariant(key[0]);
            if (c is >= 'A' and <= 'Z') { keysym = (nuint)(0x61 + (c - 'A')); return true; } // XK_a..XK_z
            if (c is >= '0' and <= '9') { keysym = c; return true; }                           // XK_0..XK_9
            return false;
        }

        if (key[0] is 'F' or 'f' && int.TryParse(key.AsSpan(1), out int n) && n is >= 1 and <= 24)
        {
            keysym = (nuint)(0xFFBE + (n - 1)); // XK_F1 = 0xFFBE
            return true;
        }

        uint ks = key.ToLowerInvariant() switch
        {
            "printscreen" => 0xFF61u, // XK_Print
            "insert" => 0xFF63u,
            "delete" => 0xFFFFu,
            "home" => 0xFF50u,
            "end" => 0xFF57u,
            "pageup" => 0xFF55u,      // XK_Page_Up
            "pagedown" => 0xFF56u,    // XK_Page_Down
            "space" => 0x20u,
            "tab" => 0xFF09u,
            "enter" => 0xFF0Du,       // XK_Return
            "escape" => 0xFF1Bu,
            "up" => 0xFF52u,
            "down" => 0xFF54u,
            "left" => 0xFF51u,
            "right" => 0xFF53u,
            _ => 0u,
        };

        keysym = ks;
        return ks != 0;
    }

    private static IReadOnlyList<HotkeyRegistration> Fail(IReadOnlyCollection<HotkeyBinding> bindings, string reason)
    {
        var results = new List<HotkeyRegistration>(bindings.Count);
        foreach (var b in bindings)
            results.Add(new HotkeyRegistration(b.Id, b.Hotkey, false, reason));
        return results;
    }
}
