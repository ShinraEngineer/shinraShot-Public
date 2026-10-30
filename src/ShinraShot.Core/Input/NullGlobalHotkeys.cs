// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Shinra CC

namespace ShinraShot.Core.Input;

/// <summary>
/// A do-nothing hotkey sink for platforms without a backend yet. It never fires and reports
/// every binding as unregistered, so the caller can surface "hotkeys unavailable here".
/// </summary>
public sealed class NullGlobalHotkeys : IGlobalHotkeys
{
    private const string Reason = "Global hotkeys are not supported on this platform.";

#pragma warning disable CS0067 // Pressed is never raised by design.
    public event Action<string>? Pressed;
#pragma warning restore CS0067

    public IReadOnlyList<HotkeyRegistration> Apply(IReadOnlyCollection<HotkeyBinding> bindings)
    {
        var results = new List<HotkeyRegistration>(bindings.Count);
        foreach (var b in bindings)
            results.Add(new HotkeyRegistration(b.Id, b.Hotkey, Ok: false, Error: Reason));
        return results;
    }

    public void Dispose() { }
}
