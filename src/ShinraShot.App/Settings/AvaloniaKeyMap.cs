// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Shinra CC

using Avalonia.Input;
using ShinraShot.Core.Settings;

namespace ShinraShot.App.Settings;

/// <summary>
/// Maps an Avalonia <see cref="Key"/> + <see cref="KeyModifiers"/> chord to a platform-neutral
/// <see cref="Hotkey"/>, so the settings UI can capture a binding the OS backends understand.
/// Returns <see langword="null"/> when the key is a bare modifier (so the capture box keeps
/// listening for the real key) or is one we don't name.
/// </summary>
internal static class AvaloniaKeyMap
{
    public static Hotkey? ToHotkey(Key key, KeyModifiers modifiers)
    {
        string? name = KeyName(key);
        if (name is null)
            return null;

        var mods = HotkeyModifiers.None;
        if (modifiers.HasFlag(KeyModifiers.Control)) mods |= HotkeyModifiers.Control;
        if (modifiers.HasFlag(KeyModifiers.Alt)) mods |= HotkeyModifiers.Alt;
        if (modifiers.HasFlag(KeyModifiers.Shift)) mods |= HotkeyModifiers.Shift;
        if (modifiers.HasFlag(KeyModifiers.Meta)) mods |= HotkeyModifiers.Meta;

        return new Hotkey(mods, name);
    }

    private static string? KeyName(Key key)
    {
        // Letters A–Z and function keys F1–F24 already stringify canonically.
        if (key is >= Key.A and <= Key.Z) return key.ToString();
        if (key is >= Key.F1 and <= Key.F24) return key.ToString();

        // Top-row digits stringify as "D0".."D9"; store the bare digit.
        if (key is >= Key.D0 and <= Key.D9) return ((char)('0' + (key - Key.D0))).ToString();

        return key switch
        {
            Key.PrintScreen => "PrintScreen",
            Key.Insert => "Insert",
            Key.Delete => "Delete",
            Key.Home => "Home",
            Key.End => "End",
            Key.PageUp => "PageUp",
            Key.PageDown => "PageDown",
            Key.Space => "Space",
            Key.Tab => "Tab",
            Key.Enter or Key.Return => "Enter",
            Key.Up => "Up",
            Key.Down => "Down",
            Key.Left => "Left",
            Key.Right => "Right",

            // Bare modifier presses: not a complete chord yet.
            Key.LeftCtrl or Key.RightCtrl
                or Key.LeftShift or Key.RightShift
                or Key.LeftAlt or Key.RightAlt
                or Key.LWin or Key.RWin => null,

            _ => null,
        };
    }
}
