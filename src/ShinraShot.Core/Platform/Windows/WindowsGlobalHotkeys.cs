// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Shinra CC

using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using ShinraShot.Core.Input;
using ShinraShot.Core.Settings;

namespace ShinraShot.Core.Platform.Windows;

/// <summary>
/// Windows global hotkeys via <c>RegisterHotKey</c>. A hidden message-only window receives the
/// resulting <c>WM_HOTKEY</c> messages; since it is created on the caller's thread (the UI
/// thread), its window procedure — and therefore <see cref="Pressed"/> — runs on that thread
/// once the app's message loop dispatches the message.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsGlobalHotkeys : IGlobalHotkeys
{
    private const int ERROR_HOTKEY_ALREADY_REGISTERED = 1409;

    private readonly string _className;
    private readonly NativeMethods.WndProcDelegate _wndProc; // kept alive for the window's lifetime
    private readonly IntPtr _hInstance;
    private readonly Dictionary<int, string> _idToAction = new();

    private IntPtr _hwnd;
    private ushort _classAtom;
    private int _nextId = 1;
    private bool _disposed;

    public event Action<string>? Pressed;

    public WindowsGlobalHotkeys()
    {
        _hInstance = NativeMethods.GetModuleHandle(null);
        _className = "ShinraShotHotkeys_" + Guid.NewGuid().ToString("N");
        _wndProc = WndProc;

        IntPtr classNamePtr = Marshal.StringToHGlobalUni(_className);
        try
        {
            var wc = new NativeMethods.WNDCLASSEX
            {
                cbSize = (uint)Marshal.SizeOf<NativeMethods.WNDCLASSEX>(),
                lpfnWndProc = Marshal.GetFunctionPointerForDelegate(_wndProc),
                hInstance = _hInstance,
                lpszClassName = classNamePtr,
            };
            _classAtom = NativeMethods.RegisterClassEx(ref wc);
        }
        finally
        {
            // RegisterClassEx copies the name into the atom table, so the buffer is no longer needed.
            Marshal.FreeHGlobal(classNamePtr);
        }

        if (_classAtom == 0)
            throw new InvalidOperationException($"RegisterClassEx failed (0x{Marshal.GetLastWin32Error():X}).");

        _hwnd = NativeMethods.CreateWindowEx(
            0, _className, null, 0, 0, 0, 0, 0,
            NativeMethods.HWND_MESSAGE, IntPtr.Zero, _hInstance, IntPtr.Zero);

        if (_hwnd == IntPtr.Zero)
        {
            int err = Marshal.GetLastWin32Error();
            NativeMethods.UnregisterClass(_className, _hInstance);
            throw new InvalidOperationException($"CreateWindowEx failed (0x{err:X}).");
        }
    }

    public IReadOnlyList<HotkeyRegistration> Apply(IReadOnlyCollection<HotkeyBinding> bindings)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ClearRegistrations();

        var results = new List<HotkeyRegistration>(bindings.Count);
        foreach (var b in bindings)
        {
            if (!b.Hotkey.IsBound)
            {
                results.Add(new HotkeyRegistration(b.Id, b.Hotkey, false, "No key assigned."));
                continue;
            }

            if (!TryMapVirtualKey(b.Hotkey.Key, out uint vk))
            {
                results.Add(new HotkeyRegistration(b.Id, b.Hotkey, false, $"Unsupported key '{b.Hotkey.Key}'."));
                continue;
            }

            uint mods = ToWin32Modifiers(b.Hotkey.Modifiers) | NativeMethods.MOD_NOREPEAT;
            int id = _nextId++;

            if (NativeMethods.RegisterHotKey(_hwnd, id, mods, vk))
            {
                _idToAction[id] = b.Id;
                results.Add(new HotkeyRegistration(b.Id, b.Hotkey, true, null));
            }
            else
            {
                int err = Marshal.GetLastWin32Error();
                string reason = err == ERROR_HOTKEY_ALREADY_REGISTERED
                    ? "Already in use by another program."
                    : $"RegisterHotKey failed (0x{err:X}).";
                results.Add(new HotkeyRegistration(b.Id, b.Hotkey, false, reason));
            }
        }

        return results;
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;

        ClearRegistrations();

        if (_hwnd != IntPtr.Zero)
        {
            NativeMethods.DestroyWindow(_hwnd);
            _hwnd = IntPtr.Zero;
        }

        if (_classAtom != 0)
        {
            NativeMethods.UnregisterClass(_className, _hInstance);
            _classAtom = 0;
        }
    }

    private void ClearRegistrations()
    {
        foreach (int id in _idToAction.Keys)
            NativeMethods.UnregisterHotKey(_hwnd, id);
        _idToAction.Clear();
    }

    private IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        if (msg == NativeMethods.WM_HOTKEY)
        {
            if (_idToAction.TryGetValue((int)wParam, out string? action))
                Pressed?.Invoke(action);
            return IntPtr.Zero;
        }

        return NativeMethods.DefWindowProc(hWnd, msg, wParam, lParam);
    }

    private static uint ToWin32Modifiers(HotkeyModifiers modifiers)
    {
        uint result = 0;
        if (modifiers.HasFlag(HotkeyModifiers.Control)) result |= NativeMethods.MOD_CONTROL;
        if (modifiers.HasFlag(HotkeyModifiers.Alt)) result |= NativeMethods.MOD_ALT;
        if (modifiers.HasFlag(HotkeyModifiers.Shift)) result |= NativeMethods.MOD_SHIFT;
        if (modifiers.HasFlag(HotkeyModifiers.Meta)) result |= NativeMethods.MOD_WIN;
        return result;
    }

    /// <summary>Map a canonical key name to a Win32 virtual-key code.</summary>
    private static bool TryMapVirtualKey(string key, out uint vk)
    {
        vk = 0;
        if (string.IsNullOrEmpty(key))
            return false;

        // Letters A–Z and digits 0–9 use their ASCII value as the VK code.
        if (key.Length == 1)
        {
            char c = char.ToUpperInvariant(key[0]);
            if (c is (>= 'A' and <= 'Z') or (>= '0' and <= '9'))
            {
                vk = c;
                return true;
            }
            return false;
        }

        // Function keys F1..F24 (VK_F1 = 0x70).
        if (key[0] is 'F' or 'f' && int.TryParse(key.AsSpan(1), out int n) && n is >= 1 and <= 24)
        {
            vk = (uint)(0x70 + (n - 1));
            return true;
        }

        vk = key.ToLowerInvariant() switch
        {
            "printscreen" => 0x2C, // VK_SNAPSHOT
            "insert" => 0x2D,
            "delete" => 0x2E,
            "home" => 0x24,
            "end" => 0x23,
            "pageup" => 0x21,
            "pagedown" => 0x22,
            "space" => 0x20,
            "tab" => 0x09,
            "enter" => 0x0D,
            "escape" => 0x1B,
            "up" => 0x26,
            "down" => 0x28,
            "left" => 0x25,
            "right" => 0x27,
            _ => 0,
        };
        return vk != 0;
    }
}
