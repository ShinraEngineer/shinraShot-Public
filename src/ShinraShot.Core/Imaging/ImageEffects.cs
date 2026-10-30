// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Shinra CC

namespace ShinraShot.Core.Imaging;

/// <summary>Pixel-level obfuscation effects applied to a (usually small) image region.</summary>
public static class ImageEffects
{
    /// <summary>Average each <paramref name="block"/>×<paramref name="block"/> cell into a flat color.</summary>
    public static CapturedImage Pixelate(CapturedImage src, int block)
    {
        block = Math.Clamp(block, 2, Math.Max(2, Math.Min(src.Width, src.Height)));
        int w = src.Width, h = src.Height, stride = src.Stride;
        var p = src.Pixels;
        var dst = (byte[])p.Clone();

        for (int by = 0; by < h; by += block)
        for (int bx = 0; bx < w; bx += block)
        {
            int ex = Math.Min(bx + block, w), ey = Math.Min(by + block, h);
            long b = 0, g = 0, r = 0, a = 0;
            int n = 0;
            for (int y = by; y < ey; y++)
            {
                int row = y * stride;
                for (int x = bx; x < ex; x++)
                {
                    int i = row + x * 4;
                    b += p[i]; g += p[i + 1]; r += p[i + 2]; a += p[i + 3];
                    n++;
                }
            }
            byte bb = (byte)(b / n), gg = (byte)(g / n), rr = (byte)(r / n), aa = (byte)(a / n);
            for (int y = by; y < ey; y++)
            {
                int row = y * stride;
                for (int x = bx; x < ex; x++)
                {
                    int i = row + x * 4;
                    dst[i] = bb; dst[i + 1] = gg; dst[i + 2] = rr; dst[i + 3] = aa;
                }
            }
        }

        return new CapturedImage(w, h, dst, src.DpiScale, src.OriginX, src.OriginY);
    }

    /// <summary>Two-pass separable box blur (a cheap gaussian approximation).</summary>
    public static CapturedImage Blur(CapturedImage src, int radius)
    {
        radius = Math.Clamp(radius, 1, 24);
        int w = src.Width, h = src.Height;
        var a = (byte[])src.Pixels.Clone();
        var b = new byte[a.Length];

        for (int pass = 0; pass < 2; pass++)
        {
            BoxH(a, b, w, h, radius);
            BoxV(b, a, w, h, radius);
        }

        return new CapturedImage(w, h, a, src.DpiScale, src.OriginX, src.OriginY);
    }

    private static void BoxH(byte[] src, byte[] dst, int w, int h, int r)
    {
        int stride = w * 4;
        for (int y = 0; y < h; y++)
        {
            int row = y * stride;
            for (int x = 0; x < w; x++)
            {
                int x0 = Math.Max(0, x - r), x1 = Math.Min(w - 1, x + r), n = x1 - x0 + 1;
                int sb = 0, sg = 0, sr = 0, sa = 0;
                for (int xx = x0; xx <= x1; xx++)
                {
                    int i = row + xx * 4;
                    sb += src[i]; sg += src[i + 1]; sr += src[i + 2]; sa += src[i + 3];
                }
                int o = row + x * 4;
                dst[o] = (byte)(sb / n); dst[o + 1] = (byte)(sg / n); dst[o + 2] = (byte)(sr / n); dst[o + 3] = (byte)(sa / n);
            }
        }
    }

    private static void BoxV(byte[] src, byte[] dst, int w, int h, int r)
    {
        int stride = w * 4;
        for (int x = 0; x < w; x++)
        {
            int col = x * 4;
            for (int y = 0; y < h; y++)
            {
                int y0 = Math.Max(0, y - r), y1 = Math.Min(h - 1, y + r), n = y1 - y0 + 1;
                int sb = 0, sg = 0, sr = 0, sa = 0;
                for (int yy = y0; yy <= y1; yy++)
                {
                    int i = yy * stride + col;
                    sb += src[i]; sg += src[i + 1]; sr += src[i + 2]; sa += src[i + 3];
                }
                int o = y * stride + col;
                dst[o] = (byte)(sb / n); dst[o + 1] = (byte)(sg / n); dst[o + 2] = (byte)(sr / n); dst[o + 3] = (byte)(sa / n);
            }
        }
    }
}
