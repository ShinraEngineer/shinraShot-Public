// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Shinra CC

using System.IO;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using ShinraShot.Core.Imaging;

namespace ShinraShot.App.Imaging;

/// <summary>Bridges <see cref="CapturedImage"/> (raw BGRA) and Avalonia bitmaps / PNG bytes.</summary>
public static class AvaloniaImageInterop
{
    private static readonly Vector StandardDpi = new(96, 96);

    /// <summary>Copy a <see cref="CapturedImage"/> into a fresh Avalonia <see cref="WriteableBitmap"/>.</summary>
    public static WriteableBitmap ToWriteableBitmap(CapturedImage img)
    {
        var wb = new WriteableBitmap(
            new PixelSize(img.Width, img.Height), StandardDpi,
            PixelFormat.Bgra8888, AlphaFormat.Premul);

        using var fb = wb.Lock();
        int srcStride = img.Stride;
        int dstStride = fb.RowBytes;

        if (srcStride == dstStride)
        {
            Marshal.Copy(img.Pixels, 0, fb.Address, img.Pixels.Length);
        }
        else
        {
            int rows = img.Height;
            for (int y = 0; y < rows; y++)
                Marshal.Copy(img.Pixels, y * srcStride, IntPtr.Add(fb.Address, y * dstStride), srcStride);
        }

        return wb;
    }

    /// <summary>Encode any Avalonia bitmap to PNG bytes (lossless, via Skia).</summary>
    public static byte[] EncodePng(Bitmap bitmap)
    {
        using var ms = new MemoryStream();
        // Bitmap.Save(Stream) defaults to PNG. The quality-taking overload is marked
        // obsolete in Avalonia 12; PNG is lossless so there is nothing to tune here.
#pragma warning disable CS0618
        bitmap.Save(ms);
#pragma warning restore CS0618
        return ms.ToArray();
    }
}
