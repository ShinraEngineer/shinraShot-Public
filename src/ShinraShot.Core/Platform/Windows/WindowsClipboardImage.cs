// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Shinra CC

using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Threading;
using ShinraShot.Core.Imaging;
using static ShinraShot.Core.Platform.Windows.NativeMethods;

namespace ShinraShot.Core.Platform.Windows;

/// <summary>
/// Writes an image to the Windows clipboard as a packed 32-bpp <c>CF_DIB</c> (for Paint,
/// Office, browsers) and, when PNG bytes are supplied, also as a registered "PNG" format
/// (for alpha-aware apps like GIMP and chat clients).
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsClipboardImage : IClipboardImage
{
    public void SetImage(CapturedImage image, byte[]? pngBytes = null)
    {
        if (!TryOpenClipboard())
            throw new InvalidOperationException("Could not open the clipboard.");

        try
        {
            EmptyClipboard();

            IntPtr hDib = BuildPackedDib(image);
            if (SetClipboardData(CF_DIB, hDib) == IntPtr.Zero)
                GlobalFree(hDib);   // ownership not transferred on failure

            if (pngBytes is { Length: > 0 })
            {
                uint cfPng = RegisterClipboardFormat("PNG");
                if (cfPng != 0)
                {
                    IntPtr hPng = AllocGlobal(pngBytes);
                    if (hPng != IntPtr.Zero && SetClipboardData(cfPng, hPng) == IntPtr.Zero)
                        GlobalFree(hPng);
                }
            }
        }
        finally
        {
            CloseClipboard();
        }
    }

    /// <summary>Build a HGLOBAL holding BITMAPINFOHEADER + bottom-up 32-bpp pixels (a CF_DIB).</summary>
    private static IntPtr BuildPackedDib(CapturedImage image)
    {
        int w = image.Width, h = image.Height;
        int imgSize = w * h * 4;
        int total = 40 + imgSize;

        var dib = new byte[total];
        var span = dib.AsSpan();
        BinaryPrimitives.WriteUInt32LittleEndian(span[0..], 40);        // biSize
        BinaryPrimitives.WriteInt32LittleEndian(span[4..], w);          // biWidth
        BinaryPrimitives.WriteInt32LittleEndian(span[8..], h);          // biHeight (+ => bottom-up)
        BinaryPrimitives.WriteUInt16LittleEndian(span[12..], 1);        // biPlanes
        BinaryPrimitives.WriteUInt16LittleEndian(span[14..], 32);       // biBitCount
        BinaryPrimitives.WriteUInt32LittleEndian(span[16..], BI_RGB);   // biCompression
        BinaryPrimitives.WriteUInt32LittleEndian(span[20..], (uint)imgSize); // biSizeImage

        // Source is top-down; CF_DIB wants bottom-up, so copy rows in reverse.
        int rowBytes = w * 4;
        for (int y = 0; y < h; y++)
            Array.Copy(image.Pixels, y * rowBytes, dib, 40 + (h - 1 - y) * rowBytes, rowBytes);

        return AllocGlobal(dib);
    }

    private static IntPtr AllocGlobal(byte[] data)
    {
        IntPtr hMem = GlobalAlloc(GMEM_MOVEABLE, (UIntPtr)data.Length);
        if (hMem == IntPtr.Zero) return IntPtr.Zero;
        IntPtr ptr = GlobalLock(hMem);
        if (ptr == IntPtr.Zero) { GlobalFree(hMem); return IntPtr.Zero; }
        try { Marshal.Copy(data, 0, ptr, data.Length); }
        finally { GlobalUnlock(hMem); }
        return hMem;
    }

    private static bool TryOpenClipboard()
    {
        for (int i = 0; i < 10; i++)
        {
            if (OpenClipboard(IntPtr.Zero)) return true;
            Thread.Sleep(10);
        }
        return false;
    }
}
