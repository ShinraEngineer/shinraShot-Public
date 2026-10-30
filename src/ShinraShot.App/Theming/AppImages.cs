// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Shinra CC

using System;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace ShinraShot.App.Theming;

/// <summary>
/// Bundled raster brand art, decoded once and shared. These are the hand-made ShinraShot icons
/// (metal-framed panels), used where a flat vector glyph would not carry the brand.
/// </summary>
public static class AppImages
{
    private static readonly Lazy<Bitmap?> UpdateReadyLazy = new(() => Load("Assets/update-ready.png"));

    /// <summary>
    /// The Shinra "UPD RDY" download panel, shown when a newer build is available (the Settings
    /// download affordance and the launch toast). Null if the asset cannot be decoded.
    /// </summary>
    public static Bitmap? UpdateReady => UpdateReadyLazy.Value;

    private static Bitmap? Load(string assetPath)
    {
        try { return new Bitmap(AssetLoader.Open(new Uri($"avares://ShinraShot/{assetPath}"))); }
        catch { return null; }
    }
}
