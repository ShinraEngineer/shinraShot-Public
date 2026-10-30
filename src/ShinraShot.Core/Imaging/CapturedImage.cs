// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Shinra CC

namespace ShinraShot.Core.Imaging;

/// <summary>
/// A raw, decoded screen grab: 32-bit BGRA pixels, top-down (row 0 is the top),
/// tightly packed (<see cref="Stride"/> == <see cref="Width"/> * 4). This is the
/// single currency between the platform capture backends and the UI/editor layer;
/// it carries no platform handles so it is safe to pass around and keep.
/// </summary>
public sealed class CapturedImage
{
    public CapturedImage(int width, int height, byte[] pixels, double dpiScale = 1.0,
                         int originX = 0, int originY = 0)
    {
        if (width <= 0 || height <= 0)
            throw new ArgumentException("Capture dimensions must be positive.");
        if (pixels.Length < (long)width * height * 4)
            throw new ArgumentException("Pixel buffer is smaller than width*height*4.");

        Width = width;
        Height = height;
        Pixels = pixels;
        DpiScale = dpiScale;
        OriginX = originX;
        OriginY = originY;
    }

    /// <summary>Width in physical pixels.</summary>
    public int Width { get; }

    /// <summary>Height in physical pixels.</summary>
    public int Height { get; }

    /// <summary>Bytes per row (always <see cref="Width"/> * 4 for this format).</summary>
    public int Stride => Width * 4;

    /// <summary>BGRA8888 pixels, top-down, length == <see cref="Stride"/> * <see cref="Height"/>.</summary>
    public byte[] Pixels { get; }

    /// <summary>DPI scale factor the grab was taken at (for metadata / editor hints).</summary>
    public double DpiScale { get; }

    /// <summary>X of this grab's top-left within the virtual desktop (physical px).</summary>
    public int OriginX { get; }

    /// <summary>Y of this grab's top-left within the virtual desktop (physical px).</summary>
    public int OriginY { get; }

    /// <summary>
    /// Force every pixel's alpha byte to 0xFF. GDI <c>BitBlt</c> leaves the alpha
    /// channel undefined (usually 0), which an alpha-aware consumer renders as fully
    /// transparent, so backends that use GDI must call this after a grab.
    /// </summary>
    public void ForceOpaque()
    {
        var p = Pixels;
        for (int i = 3; i < p.Length; i += 4)
            p[i] = 0xFF;
    }

    /// <summary>
    /// Return a new <see cref="CapturedImage"/> for the given sub-rectangle (image-local
    /// pixels). The rectangle is clamped to the bounds; width/height collapse to at least 1.
    /// </summary>
    public CapturedImage Crop(int x, int y, int w, int h)
    {
        x = Math.Clamp(x, 0, Width - 1);
        y = Math.Clamp(y, 0, Height - 1);
        w = Math.Clamp(w, 1, Width - x);
        h = Math.Clamp(h, 1, Height - y);

        var dst = new byte[w * h * 4];
        int srcStride = Stride, dstStride = w * 4;
        for (int row = 0; row < h; row++)
            Array.Copy(Pixels, (y + row) * srcStride + x * 4, dst, row * dstStride, dstStride);

        return new CapturedImage(w, h, dst, DpiScale, OriginX + x, OriginY + y);
    }

    /// <summary>Hex color (#RRGGBB) of the pixel at the given image-local coordinate.</summary>
    public string ColorHexAt(int x, int y)
    {
        x = Math.Clamp(x, 0, Width - 1);
        y = Math.Clamp(y, 0, Height - 1);
        int i = y * Stride + x * 4;
        return $"#{Pixels[i + 2]:X2}{Pixels[i + 1]:X2}{Pixels[i]:X2}";
    }
}
