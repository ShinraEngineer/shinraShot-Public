// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Shinra CC

using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using ShinraShot.Core.Capture;
using ShinraShot.Core.Imaging;
using static ShinraShot.Core.Platform.Linux.X11Native;

namespace ShinraShot.Core.Platform.Linux;

/// <summary>
/// X11 screen capture via Xlib <c>XGetImage</c> on the root window, with per-monitor
/// geometry from the RandR extension. All coordinates are virtual-desktop physical
/// pixels; on X11 the root window spans the whole virtual desktop starting at (0,0),
/// so virtual coordinates and root-window coordinates coincide.
///
/// This also serves Wayland sessions through XWayland on a best-effort basis (the
/// native Wayland path — <c>org.freedesktop.portal.Screenshot</c> — is a later slice).
/// Each call opens and closes its own display connection, mirroring the stateless
/// Windows backend; a cached connection for the live magnifier is a later refinement.
/// Scale is the desktop's global <c>Xft.dpi</c> (dpi / 96), applied to every monitor:
/// X11 has no portable per-output scale factor, and desktops express HiDPI through that
/// one setting. Grabs are always in physical pixels (what the overlay expects); scale is
/// UI metadata only.
/// </summary>
[SupportedOSPlatform("linux")]
public sealed class LinuxCaptureBackend : ICaptureBackend
{
    public ScreenRect VirtualBounds
    {
        get
        {
            nint dpy = OpenDisplayOrThrow();
            try
            {
                int screen = XDefaultScreen(dpy);
                // The X11 root window is the full virtual desktop, anchored at (0,0).
                return new ScreenRect(0, 0, XDisplayWidth(dpy, screen), XDisplayHeight(dpy, screen));
            }
            finally { XCloseDisplay(dpy); }
        }
    }

    public IReadOnlyList<ScreenInfo> GetScreens()
    {
        nint dpy = OpenDisplayOrThrow();
        try
        {
            int screen = XDefaultScreen(dpy);
            nint root = XRootWindow(dpy, screen);
            double scale = GetGlobalScale(dpy);

            var monitors = TryGetRandrScreens(dpy, root, scale);
            if (monitors.Count > 0)
                return monitors;

            // RandR unavailable (or no active CRTC): one screen = the whole root.
            var bounds = new ScreenRect(0, 0, XDisplayWidth(dpy, screen), XDisplayHeight(dpy, screen));
            return new[] { new ScreenInfo(bounds, scale, IsPrimary: true, "X11") };
        }
        finally { XCloseDisplay(dpy); }
    }

    public CapturedImage Capture(ScreenRect region)
    {
        if (region.IsEmpty)
            throw new ArgumentException("Capture region is empty.", nameof(region));

        nint dpy = OpenDisplayOrThrow();
        try
        {
            int screen = XDefaultScreen(dpy);
            nint root = XRootWindow(dpy, screen);

            // Clamp to the root so XGetImage cannot raise BadMatch on an out-of-bounds grab.
            var full = new ScreenRect(0, 0, XDisplayWidth(dpy, screen), XDisplayHeight(dpy, screen));
            var r = Intersect(region, full);
            if (r.IsEmpty)
                throw new ArgumentException("Capture region lies outside the virtual desktop.", nameof(region));

            nint imgPtr = XGetImage(dpy, root, r.X, r.Y, (uint)r.Width, (uint)r.Height, AllPlanes, ZPixmap);
            if (imgPtr == 0)
                throw new InvalidOperationException("XGetImage failed (no image returned).");

            try
            {
                var img = Marshal.PtrToStructure<XImage>(imgPtr);
                byte[] bgra = ToBgra(img);
                var captured = new CapturedImage(img.width, img.height, bgra, dpiScale: 1.0, originX: r.X, originY: r.Y);
                captured.ForceOpaque(); // X grabs carry no meaningful alpha.
                return captured;
            }
            finally
            {
                // Replicate XDestroyImage for an XGetImage result: free the pixel buffer,
                // then the struct. (XDestroyImage is an Xlib macro, not an exported symbol.)
                var img = Marshal.PtrToStructure<XImage>(imgPtr);
                if (img.data != 0) XFree(img.data);
                XFree(imgPtr);
            }
        }
        finally { XCloseDisplay(dpy); }
    }

    // ---- helpers ------------------------------------------------------------

    private static nint OpenDisplayOrThrow()
    {
        nint dpy = XOpenDisplay(null);
        if (dpy == 0)
            throw new InvalidOperationException(
                "XOpenDisplay failed — no reachable X11 display ($DISPLAY). " +
                "On a pure Wayland session the portal capture path (a later slice) is required.");
        return dpy;
    }

    private static ScreenRect Intersect(ScreenRect a, ScreenRect b)
    {
        int left = Math.Max(a.X, b.X);
        int top = Math.Max(a.Y, b.Y);
        int right = Math.Min(a.Right, b.Right);
        int bottom = Math.Min(a.Bottom, b.Bottom);
        return (right <= left || bottom <= top)
            ? default
            : new ScreenRect(left, top, right - left, bottom - top);
    }

    /// <summary>
    /// The desktop scale factor, from the <c>Xft.dpi</c> X resource (dpi / 96). This is the
    /// portable X11 HiDPI signal GTK/Qt honour; X11 has no per-output scale, so it applies
    /// to every monitor. Defaults to 1.0 when the resource is absent or unparseable, and is
    /// clamped to a sane range.
    /// </summary>
    private static double GetGlobalScale(nint dpy)
    {
        try
        {
            nint rm = XResourceManagerString(dpy);   // owned by Xlib — do not free
            if (rm == 0)
                return 1.0;
            string? resources = Marshal.PtrToStringAnsi(rm);
            if (string.IsNullOrEmpty(resources))
                return 1.0;

            foreach (string line in resources.Split('\n'))
            {
                int colon = line.IndexOf(':');
                if (colon <= 0)
                    continue;
                if (line.Substring(0, colon).Trim() != "Xft.dpi")
                    continue;
                if (double.TryParse(line.Substring(colon + 1).Trim(), out double dpi) && dpi > 0)
                    return Math.Clamp(dpi / 96.0, 0.5, 4.0);
            }
        }
        catch { /* resource string malformed: fall back to 1.0 */ }
        return 1.0;
    }

    /// <summary>Enumerate active RandR CRTCs as monitors. Empty list if RandR is missing.</summary>
    private static List<ScreenInfo> TryGetRandrScreens(nint dpy, nint root, double scale)
    {
        var list = new List<ScreenInfo>();
        nint resPtr;
        try
        {
            resPtr = XRRGetScreenResourcesCurrent(dpy, root);
        }
        catch (DllNotFoundException) { return list; }   // libXrandr not installed
        catch (EntryPointNotFoundException) { return list; }

        if (resPtr == 0)
            return list;

        try
        {
            var res = Marshal.PtrToStructure<XRRScreenResources>(resPtr);
            for (int i = 0; i < res.ncrtc; i++)
            {
                nint crtc = Marshal.ReadIntPtr(res.crtcs, i * nint.Size);
                nint ciPtr = XRRGetCrtcInfo(dpy, resPtr, crtc);
                if (ciPtr == 0)
                    continue;
                try
                {
                    var ci = Marshal.PtrToStructure<XRRCrtcInfo>(ciPtr);
                    if (ci.mode == 0 || ci.width == 0 || ci.height == 0)
                        continue; // CRTC is disabled / not driving an output

                    var bounds = new ScreenRect(ci.x, ci.y, (int)ci.width, (int)ci.height);
                    // Heuristic primary: X11 conventionally anchors the primary at (0,0).
                    // Proper RRGetOutputPrimary → output → CRTC mapping is a later refinement.
                    bool primary = ci.x == 0 && ci.y == 0;
                    list.Add(new ScreenInfo(bounds, scale, primary, $"XRandR-{i}"));
                }
                finally { XRRFreeCrtcInfo(ciPtr); }
            }

            if (list.Count > 0 && !list.Any(s => s.IsPrimary))
                list[0] = list[0] with { IsPrimary = true };
        }
        finally { XRRFreeScreenResources(resPtr); }

        return list;
    }

    /// <summary>
    /// Convert an Xlib <see cref="XImage"/> to tightly-packed top-down BGRA8888. Fast path
    /// for the ubiquitous 32-bpp little-endian BGRX visual (a straight per-row copy); a
    /// general mask-driven path otherwise (16/24/32 bpp, either byte order).
    /// </summary>
    private static byte[] ToBgra(in XImage img)
    {
        int w = img.width, h = img.height;
        int stride = img.bytes_per_line;
        int bpp = img.bits_per_pixel;

        // Copy the server buffer into managed memory once (avoids /unsafe for the whole project).
        var src = new byte[(long)stride * h];
        Marshal.Copy(img.data, src, 0, src.Length);

        var dst = new byte[(long)w * h * 4];

        ulong rMask = (ulong)img.red_mask, gMask = (ulong)img.green_mask, bMask = (ulong)img.blue_mask;
        bool lsb = img.byte_order == LSBFirst;

        bool fastBgrx = bpp == 32 && lsb &&
                        rMask == 0x00FF0000UL && gMask == 0x0000FF00UL && bMask == 0x000000FFUL;
        if (fastBgrx)
        {
            // Memory bytes are already B,G,R,X per pixel; copy each row's w*4 payload.
            int rowBytes = w * 4;
            for (int y = 0; y < h; y++)
                Array.Copy(src, (long)y * stride, dst, (long)y * rowBytes, rowBytes);
            return dst; // alpha fixed up by ForceOpaque at the call site
        }

        // General path: pull each channel through its mask and scale to 8 bits.
        var r = new MaskChannel(rMask);
        var g = new MaskChannel(gMask);
        var b = new MaskChannel(bMask);
        int bytesPerPixel = Math.Max(1, bpp / 8);

        for (int y = 0; y < h; y++)
        {
            int srcRow = y * stride;
            int dstRow = y * w * 4;
            for (int x = 0; x < w; x++)
            {
                ulong px = ReadPixel(src, srcRow + x * bytesPerPixel, bytesPerPixel, lsb);
                int di = dstRow + x * 4;
                dst[di + 0] = b.Extract(px);
                dst[di + 1] = g.Extract(px);
                dst[di + 2] = r.Extract(px);
                dst[di + 3] = 0xFF;
            }
        }
        return dst;
    }

    private static ulong ReadPixel(byte[] src, int offset, int bytes, bool lsb)
    {
        ulong v = 0;
        if (lsb)
            for (int i = 0; i < bytes; i++) v |= (ulong)src[offset + i] << (8 * i);
        else
            for (int i = 0; i < bytes; i++) v = (v << 8) | src[offset + i];
        return v;
    }

    /// <summary>A colour channel described by its pixel mask: shift to the low bits, scale to 0..255.</summary>
    private readonly struct MaskChannel
    {
        private readonly ulong _mask;
        private readonly int _shift;
        private readonly uint _max;

        public MaskChannel(ulong mask)
        {
            _mask = mask;
            if (mask == 0) { _shift = 0; _max = 1; return; }
            _shift = System.Numerics.BitOperations.TrailingZeroCount(mask);
            _max = (uint)(mask >> _shift);
            if (_max == 0) _max = 1;
        }

        public byte Extract(ulong pixel)
        {
            if (_mask == 0) return 0;
            uint v = (uint)((pixel & _mask) >> _shift);
            return (byte)(v * 255 / _max);
        }
    }
}
