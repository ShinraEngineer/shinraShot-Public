// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Shinra CC

using System.Text;

namespace ShinraShot.Core.Settings;

/// <summary>Modifier keys that can accompany a global hotkey.</summary>
[Flags]
public enum HotkeyModifiers
{
    None = 0,
    Control = 1 << 0,
    Alt = 1 << 1,
    Shift = 1 << 2,

    /// <summary>The Windows / Super / Command key.</summary>
    Meta = 1 << 3,
}

/// <summary>
/// A platform-neutral description of a global hotkey: zero or more modifiers plus a single
/// non-modifier key, named canonically (e.g. <c>PrintScreen</c>, <c>F11</c>, <c>A</c>). The
/// OS-specific backends (Win32 <c>RegisterHotKey</c>, X11 <c>XGrabKey</c>) map the name to a
/// virtual-key code or keysym. An empty <see cref="Key"/> means "unbound".
/// </summary>
public readonly record struct Hotkey(HotkeyModifiers Modifiers, string Key)
{
    /// <summary>An unbound hotkey (no key, no modifiers).</summary>
    public static Hotkey None => new(HotkeyModifiers.None, string.Empty);

    /// <summary>True when a non-modifier key is set, i.e. the hotkey can be registered.</summary>
    public bool IsBound => !string.IsNullOrWhiteSpace(Key);

    /// <summary>Parse a string such as <c>"Ctrl+Shift+PrintScreen"</c>; throws on malformed input.</summary>
    public static Hotkey Parse(string text)
        => TryParse(text, out var hk) ? hk : throw new FormatException($"Not a valid hotkey: '{text}'.");

    /// <summary>
    /// Parse a string such as <c>"Ctrl+PrintScreen"</c>. An empty/whitespace string parses as
    /// <see cref="None"/> (unbound) and returns true. Returns false only for malformed input
    /// (unknown layout, two non-modifier keys, or modifiers with no key).
    /// </summary>
    public static bool TryParse(string? text, out Hotkey hotkey)
    {
        hotkey = None;
        if (string.IsNullOrWhiteSpace(text))
            return true; // unbound is valid

        var mods = HotkeyModifiers.None;
        string? key = null;

        foreach (var raw in text.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            switch (raw.ToLowerInvariant())
            {
                case "ctrl" or "control": mods |= HotkeyModifiers.Control; break;
                case "alt": mods |= HotkeyModifiers.Alt; break;
                case "shift": mods |= HotkeyModifiers.Shift; break;
                case "win" or "meta" or "super" or "cmd" or "command": mods |= HotkeyModifiers.Meta; break;
                default:
                    if (key is not null) return false; // only one non-modifier key allowed
                    key = Canonicalize(raw);
                    break;
            }
        }

        if (key is null) return false; // modifiers without a key cannot be a hotkey
        hotkey = new Hotkey(mods, key);
        return true;
    }

    /// <summary>Render as <c>"Ctrl+Alt+Shift+Meta+Key"</c>, modifiers first in a stable order.</summary>
    public override string ToString()
    {
        if (!IsBound && Modifiers == HotkeyModifiers.None)
            return string.Empty;

        var sb = new StringBuilder();
        if (Modifiers.HasFlag(HotkeyModifiers.Control)) sb.Append("Ctrl+");
        if (Modifiers.HasFlag(HotkeyModifiers.Alt)) sb.Append("Alt+");
        if (Modifiers.HasFlag(HotkeyModifiers.Shift)) sb.Append("Shift+");
        if (Modifiers.HasFlag(HotkeyModifiers.Meta)) sb.Append("Meta+");
        sb.Append(Key);
        return sb.ToString();
    }

    // Normalize key-name casing so persisted values and backend lookups agree. Single
    // characters uppercase (a -> A); known special keys take their canonical spelling;
    // anything else is upper-cased, which also covers F1..F24 and digits.
    private static string Canonicalize(string key)
    {
        if (key.Length == 1)
            return key.ToUpperInvariant();

        return s_known.TryGetValue(key.ToLowerInvariant(), out var canonical)
            ? canonical
            : key.ToUpperInvariant();
    }

    private static readonly Dictionary<string, string> s_known = new(StringComparer.OrdinalIgnoreCase)
    {
        ["printscreen"] = "PrintScreen",
        ["prtsc"] = "PrintScreen",
        ["insert"] = "Insert",
        ["delete"] = "Delete",
        ["home"] = "Home",
        ["end"] = "End",
        ["pageup"] = "PageUp",
        ["pagedown"] = "PageDown",
        ["space"] = "Space",
        ["tab"] = "Tab",
        ["enter"] = "Enter",
        ["escape"] = "Escape",
        ["up"] = "Up",
        ["down"] = "Down",
        ["left"] = "Left",
        ["right"] = "Right",
    };
}
