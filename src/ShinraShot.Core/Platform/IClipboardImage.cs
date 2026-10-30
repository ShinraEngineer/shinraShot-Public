// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Shinra CC

using ShinraShot.Core.Imaging;

namespace ShinraShot.Core.Platform;

/// <summary>Places an image on the system clipboard.</summary>
public interface IClipboardImage
{
    /// <summary>
    /// Copy <paramref name="image"/> to the clipboard. When <paramref name="pngBytes"/>
    /// is supplied, a "PNG" clipboard format is offered as well so alpha-aware consumers
    /// (GIMP, chat apps) get a lossless, alpha-correct paste alongside the DIB.
    /// </summary>
    void SetImage(CapturedImage image, byte[]? pngBytes = null);
}
