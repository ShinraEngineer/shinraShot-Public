// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Shinra CC

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using ShinraShot.App.Theming;
using ShinraShot.Core.Settings;

namespace ShinraShot.App.Settings;

/// <summary>
/// A focusable field that captures a global-hotkey chord. Click it (or tab to it) to start
/// listening; the next non-modifier key press — together with whatever modifiers are held —
/// becomes the binding. Backspace/Delete clears it; Escape bubbles out so a dialog can cancel.
/// </summary>
internal sealed class HotkeyBox : Border
{
    private readonly TextBlock _label = new()
    {
        VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
    };

    private bool _listening;
    private Hotkey _value;

    /// <summary>Raised whenever <see cref="Value"/> changes through user capture or clearing.</summary>
    public event EventHandler? ValueChanged;

    public HotkeyBox()
    {
        Focusable = true;
        Background = Palette.FieldBrush;
        BorderBrush = Palette.BorderBrush;
        BorderThickness = new Thickness(1);
        CornerRadius = new CornerRadius(4);
        Padding = new Thickness(10, 7);
        MinWidth = 190;
        Cursor = new Cursor(StandardCursorType.Hand);
        Child = _label;

        GotFocus += (_, _) => SetListening(true);
        LostFocus += (_, _) => SetListening(false);
        UpdateLabel();
    }

    public Hotkey Value
    {
        get => _value;
        set { _value = value; UpdateLabel(); }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        Focus();
    }

    private void SetListening(bool on)
    {
        _listening = on;
        BorderBrush = on ? Palette.AccentBrush : Palette.BorderBrush;
        UpdateLabel();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (!_listening)
        {
            base.OnKeyDown(e);
            return;
        }

        // Let Escape bubble so the enclosing dialog can cancel.
        if (e.Key == Key.Escape)
            return;

        e.Handled = true;

        if (e.Key is Key.Back or Key.Delete)
        {
            Value = Hotkey.None;
            ValueChanged?.Invoke(this, EventArgs.Empty);
            return;
        }

        if (AvaloniaKeyMap.ToHotkey(e.Key, e.KeyModifiers) is { IsBound: true } bound)
        {
            Value = bound;
            ValueChanged?.Invoke(this, EventArgs.Empty);
        }
        // A bare modifier keeps us listening for the real key.
    }

    private void UpdateLabel()
    {
        if (_value.IsBound)
        {
            // Show a captured/existing chord immediately, even while the field still holds focus,
            // so pressing the keys gives instant confirmation that the binding was accepted.
            _label.Text = _value.ToString();
            _label.Foreground = Palette.TextBrush;
        }
        else if (_listening)
        {
            _label.Text = "Press a key…";
            _label.Foreground = Palette.SecondaryBrush;
        }
        else
        {
            _label.Text = "(unbound)";
            _label.Foreground = Palette.SecondaryBrush;
        }
    }
}
