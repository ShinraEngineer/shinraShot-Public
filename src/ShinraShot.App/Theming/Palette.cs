// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Shinra CC

using Avalonia;
using Avalonia.Media;

namespace ShinraShot.App.Theming;

/// <summary>
/// ShinraShot's dark palette, ported from the WindowsUpdateBlocker "glass card" theme
/// (blue-gray cards, near-white text, subtle white borders) with the ShinraShot red accent.
/// App.axaml carries the matching XAML resources (window background + Fluent accent); this is
/// the single source for the code-built windows (editor, action menu, toast).
/// </summary>
public static class Palette
{
    public static readonly Color Bg = Color.FromRgb(0x22, 0x28, 0x34);
    public static readonly Color CardTop = Color.FromRgb(0x3A, 0x42, 0x52);
    public static readonly Color CardBottom = Color.FromRgb(0x22, 0x28, 0x34);
    public static readonly Color CanvasBackdrop = Color.FromRgb(0x18, 0x1C, 0x24);
    public static readonly Color Text = Color.FromRgb(0xF2, 0xF5, 0xFA);
    public static readonly Color TextSecondary = Color.FromRgb(0xA7, 0xB0, 0xBF);
    public static readonly Color Border = Color.FromArgb(0x33, 0xFF, 0xFF, 0xFF);
    public static readonly Color Field = Color.FromRgb(0x2B, 0x32, 0x40);
    public static readonly Color Accent = Color.FromRgb(0xE0, 0x2B, 0x2B);

    public static IBrush BgBrush => new SolidColorBrush(Bg);
    public static IBrush TextBrush => new SolidColorBrush(Text);
    public static IBrush SecondaryBrush => new SolidColorBrush(TextSecondary);
    public static IBrush BorderBrush => new SolidColorBrush(Border);
    public static IBrush FieldBrush => new SolidColorBrush(Field);
    public static IBrush AccentBrush => new SolidColorBrush(Accent);
    public static IBrush CanvasBrush => new SolidColorBrush(CanvasBackdrop);

    public static IBrush CardBrush => new LinearGradientBrush
    {
        StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
        EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
        GradientStops =
        {
            new GradientStop(CardTop, 0),
            new GradientStop(CardBottom, 1),
        },
    };
}
