// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Shinra CC

using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using ShinraShot.Core.Capture;
using ShinraShot.Core.Imaging;
using static ShinraShot.Core.Platform.Windows.NativeMethods;

namespace ShinraShot.Core.Platform.Windows;

/// <summary>
/// GDI-based screen capture. Works on single- and same-DPI multi-monitor setups at
/// native resolution because the process is Per-Monitor-V2 DPI-aware (set by Avalonia).
/// Mixed-DPI exactness and DXGI desktop duplication are a later refinement (ROADMAP S8).
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsCaptureBackend : ICaptureBackend
{
    public ScreenRect VirtualBounds
    {
        get
        {
            int x = GetSystemMetrics(SM_XVIRTUALSCREEN);
            int y = GetSystemMetrics(SM_YVIRTUALSCREEN);
            int w = GetSystemMetrics(SM_CXVIRTUALSCREEN);
            int h = GetSystemMetrics(SM_CYVIRTUALSCREEN);
            return new ScreenRect(x, y, w, h);
        }
    }

    public IReadOnlyList<ScreenInfo> GetScreens()
    {
        var list = new List<ScreenInfo>();
        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr hMon, IntPtr _, ref RECT _, IntPtr _) =>
        {
            var mi = new MONITORINFOEX { cbSize = (uint)Marshal.SizeOf<MONITORINFOEX>() };
            if (GetMonitorInfo(hMon, ref mi))
            {
                var b = ScreenRect.FromLtrb(mi.rcMonitor.Left, mi.rcMonitor.Top, mi.rcMonitor.Right, mi.rcMonitor.Bottom);
                double scale = 1.0;
                try
                {
                    if (GetDpiForMonitor(hMon, MDT_EFFECTIVE_DPI, out uint dpiX, out _) == 0 && dpiX > 0)
                        scale = dpiX / 96.0;
                }
                catch { /* shcore unavailable: leave scale at 1.0 */ }

                bool primary = (mi.dwFlags & MONITORINFOF_PRIMARY) != 0;
                list.Add(new ScreenInfo(b, scale, primary, mi.szDevice));
            }
            return true;
        }, IntPtr.Zero);
        return list;
    }

    public CapturedImage Capture(ScreenRect region)
    {
        if (region.IsEmpty)
            throw new ArgumentException("Capture region is empty.", nameof(region));

        int w = region.Width, h = region.Height;
        IntPtr hScreen = GetDC(IntPtr.Zero);
        if (hScreen == IntPtr.Zero)
            throw new InvalidOperationException("GetDC(NULL) failed.");

        IntPtr hDC = IntPtr.Zero, hBmp = IntPtr.Zero, old = IntPtr.Zero;
        try
        {
            hDC = CreateCompatibleDC(hScreen);
            var bmi = new BITMAPINFOHEADER
            {
                biSize = (uint)Marshal.SizeOf<BITMAPINFOHEADER>(),
                biWidth = w,
                biHeight = -h,               // negative => top-down rows
                biPlanes = 1,
                biBitCount = 32,
                biCompression = BI_RGB,
            };
            hBmp = CreateDIBSection(hDC, ref bmi, DIB_RGB_COLORS, out IntPtr bits, IntPtr.Zero, 0);
            if (hBmp == IntPtr.Zero || bits == IntPtr.Zero)
                throw new InvalidOperationException("CreateDIBSection failed.");

            old = SelectObject(hDC, hBmp);
            if (!BitBlt(hDC, 0, 0, w, h, hScreen, region.X, region.Y, SRCCOPY | CAPTUREBLT))
                throw new InvalidOperationException("BitBlt failed.");

            var buffer = new byte[(long)w * h * 4];
            Marshal.Copy(bits, buffer, 0, buffer.Length);

            var img = new CapturedImage(w, h, buffer, originX: region.X, originY: region.Y);
            img.ForceOpaque();   // GDI leaves alpha undefined
            return img;
        }
        finally
        {
            if (old != IntPtr.Zero) SelectObject(hDC, old);
            if (hBmp != IntPtr.Zero) DeleteObject(hBmp);
            if (hDC != IntPtr.Zero) DeleteDC(hDC);
            ReleaseDC(IntPtr.Zero, hScreen);
        }
    }
}
