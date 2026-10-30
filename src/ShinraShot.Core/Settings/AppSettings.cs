// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Shinra CC

using ShinraShot.Core.Capture;

namespace ShinraShot.Core.Settings;

/// <summary>
/// The persisted, user-editable settings for ShinraShot. This is the schema of
/// <c>settings.json</c> (see <see cref="SettingsStore"/> / <see cref="SettingsPaths"/>);
/// the App layer reads it at startup and writes it from the settings window.
/// </summary>
public sealed class AppSettings
{
    /// <summary>Schema version, bumped when a migration is needed. Currently always 1.</summary>
    public int Version { get; set; } = 1;

    /// <summary>What happens to a capture once it is taken.</summary>
    public DefaultCaptureAction DefaultAction { get; set; } = DefaultCaptureAction.Ask;

    /// <summary>
    /// Folder for auto-saved captures. <see langword="null"/> means the built-in default
    /// (<c>Pictures/ShinraShot</c>); the App layer resolves it.
    /// </summary>
    public string? SaveDirectory { get; set; }

    /// <summary>Include the mouse cursor in captures. Reserved for a later slice.</summary>
    public bool CaptureCursor { get; set; }

    /// <summary>Master switch for the OS-wide capture hotkeys.</summary>
    public bool GlobalHotkeysEnabled { get; set; } = true;

    /// <summary>Hotkey that starts a region capture. Default <c>PrintScreen</c>.</summary>
    public Hotkey RegionHotkey { get; set; } = new(HotkeyModifiers.None, "PrintScreen");

    /// <summary>Hotkey that takes a full-screen capture. Default <c>Ctrl+PrintScreen</c>.</summary>
    public Hotkey FullScreenHotkey { get; set; } = new(HotkeyModifiers.Control, "PrintScreen");

    /// <summary>A deep copy, so the settings window can edit a draft and discard on cancel.</summary>
    public AppSettings Clone() => (AppSettings)MemberwiseClone();
}
