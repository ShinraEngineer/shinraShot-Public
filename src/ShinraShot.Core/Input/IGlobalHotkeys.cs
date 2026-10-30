// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Shinra CC

using ShinraShot.Core.Settings;

namespace ShinraShot.Core.Input;

/// <summary>One hotkey to register, tagged with a caller-defined action id (e.g. "region").</summary>
/// <param name="Id">Stable identifier echoed back by <see cref="IGlobalHotkeys.Pressed"/>.</param>
/// <param name="Hotkey">The chord to grab.</param>
public readonly record struct HotkeyBinding(string Id, Hotkey Hotkey);

/// <summary>The outcome of trying to register one <see cref="HotkeyBinding"/>.</summary>
/// <param name="Id">The binding's action id.</param>
/// <param name="Hotkey">The chord that was attempted.</param>
/// <param name="Ok">True when the OS accepted the registration.</param>
/// <param name="Error">A short reason when <paramref name="Ok"/> is false (e.g. already in use).</param>
public readonly record struct HotkeyRegistration(string Id, Hotkey Hotkey, bool Ok, string? Error);

/// <summary>
/// OS-wide capture hotkeys. The App registers a set and is notified, by action id, when one is
/// pressed. <see cref="Pressed"/> may be raised on an arbitrary thread, so UI work must be
/// marshalled by the subscriber. Implementations are platform-specific (Win32 <c>RegisterHotKey</c>,
/// X11 <c>XGrabKey</c>); see <c>PlatformServices.CreateGlobalHotkeys</c>.
/// </summary>
public interface IGlobalHotkeys : IDisposable
{
    /// <summary>Raised with the action id of a registered hotkey when it fires.</summary>
    event Action<string>? Pressed;

    /// <summary>
    /// Replace all current registrations with <paramref name="bindings"/> (an empty set clears
    /// everything). Unbound or unmappable chords are skipped. Returns one result per binding.
    /// </summary>
    IReadOnlyList<HotkeyRegistration> Apply(IReadOnlyCollection<HotkeyBinding> bindings);
}
