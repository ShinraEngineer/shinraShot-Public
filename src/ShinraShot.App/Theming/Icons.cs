// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Shinra CC

using Avalonia;
using Avalonia.Media;
using ShapePath = Avalonia.Controls.Shapes.Path;

namespace ShinraShot.App.Theming;

/// <summary>
/// Small vector glyphs drawn as stroked paths, so they stay crisp at any size and take the
/// theme colour. Paths use the 24x24 stroke outlines from the Tabler icon set, matching the icon
/// family the sibling apps use for their update/download affordance.
/// </summary>
public static class Icons
{
    // Tabler "download": a tray, a down chevron, and a vertical shaft (the arrow into the tray).
    private const string DownloadData =
        "M4 17v2a2 2 0 0 0 2 2h12a2 2 0 0 0 2 -2v-2 M7 11l5 5l5 -5 M12 4l0 12";

    private static readonly Geometry DownloadGeometry = Geometry.Parse(DownloadData);

    /// <summary>A download glyph sized to <paramref name="size"/> DIP, stroked in <paramref name="brush"/>.</summary>
    public static ShapePath Download(double size, IBrush brush, double thickness = 2) => new()
    {
        Data = DownloadGeometry,
        Stroke = brush,
        StrokeThickness = thickness,
        StrokeJoin = PenLineJoin.Round,
        StrokeLineCap = PenLineCap.Round,
        Fill = null,
        Stretch = Stretch.Uniform,
        Width = size,
        Height = size,
    };
}
