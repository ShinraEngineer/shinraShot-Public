// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Shinra CC

namespace ShinraShot.Core.Capture;

/// <summary>A rectangle in virtual-desktop physical pixels (X/Y may be negative).</summary>
public readonly record struct ScreenRect(int X, int Y, int Width, int Height)
{
    public int Right => X + Width;
    public int Bottom => Y + Height;
    public bool IsEmpty => Width <= 0 || Height <= 0;

    public static ScreenRect FromLtrb(int left, int top, int right, int bottom)
        => new(left, top, right - left, bottom - top);
}

/// <summary>Geometry and scale of a single display.</summary>
public sealed record ScreenInfo(ScreenRect Bounds, double Scale, bool IsPrimary, string Name);
