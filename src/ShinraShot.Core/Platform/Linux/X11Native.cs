// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Shinra CC

using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace ShinraShot.Core.Platform.Linux;

/// <summary>
/// P/Invoke surface for Xlib (<c>libX11</c>) and the RandR extension (<c>libXrandr</c>),
/// used by <see cref="LinuxCaptureBackend"/>. Deliberately small — only what a root-window
/// grab plus per-monitor geometry needs. XIDs and server pointers are opaque
/// <see cref="nint"/>s; the few structs we read back are declared with the exact 64-bit
/// (LP64) field layout so <see cref="Marshal.PtrToStructure{T}(nint)"/> lands every field.
/// ShinraShot is x86-64/arm64 only, so <c>long</c>/<c>unsigned long</c> are 8 bytes.
/// </summary>
[SupportedOSPlatform("linux")]
internal static class X11Native
{
    private const string X11 = "libX11.so.6";
    private const string Xrandr = "libXrandr.so.2";

    /// <summary>XGetImage <c>format</c>: packed pixels, one plane.</summary>
    public const int ZPixmap = 2;

    /// <summary>XGetImage <c>plane_mask</c>: every bit plane.</summary>
    public static readonly nuint AllPlanes = unchecked((nuint)~0UL);

    /// <summary>XImage <c>byte_order</c> / <c>bitmap_bit_order</c> values.</summary>
    public const int LSBFirst = 0;
    public const int MSBFirst = 1;

    // ---- libX11 -------------------------------------------------------------

    /// <summary>Open a connection; pass <c>null</c> for the <c>$DISPLAY</c> default. NULL on failure.</summary>
    [DllImport(X11, CharSet = CharSet.Ansi)]
    public static extern nint XOpenDisplay(string? displayName);

    [DllImport(X11)]
    public static extern int XCloseDisplay(nint display);

    [DllImport(X11)]
    public static extern int XDefaultScreen(nint display);

    [DllImport(X11)]
    public static extern nint XRootWindow(nint display, int screen);

    [DllImport(X11)]
    public static extern int XDisplayWidth(nint display, int screen);

    [DllImport(X11)]
    public static extern int XDisplayHeight(nint display, int screen);

    /// <summary>Grab a rectangle of a drawable. Returns a malloc'd <see cref="XImage"/>* (NULL on failure).</summary>
    [DllImport(X11)]
    public static extern nint XGetImage(nint display, nint drawable,
        int x, int y, uint width, uint height, nuint planeMask, int format);

    /// <summary>Free Xlib-allocated memory (we use it to tear down an <see cref="XGetImage"/> result).</summary>
    [DllImport(X11)]
    public static extern int XFree(nint data);

    /// <summary>
    /// The display's resource-manager string (the merged X resources, e.g. the
    /// <c>Xft.dpi</c> the desktop sets for scaling). Returns a pointer owned by Xlib —
    /// read it, never free it. NULL when no resources are set.
    /// </summary>
    [DllImport(X11, CharSet = CharSet.Ansi)]
    public static extern nint XResourceManagerString(nint display);

    // ---- global hotkeys (XGrabKey + an event loop) --------------------------

    public const int GrabModeSync = 0;
    public const int GrabModeAsync = 1;

    /// <summary>XEvent <c>type</c> for a key-press.</summary>
    public const int KeyPress = 2;

    // X core modifier mask bits.
    public const uint ShiftMask = 1 << 0;
    public const uint LockMask = 1 << 1;   // CapsLock
    public const uint ControlMask = 1 << 2;
    public const uint Mod1Mask = 1 << 3;   // Alt
    public const uint Mod2Mask = 1 << 4;   // NumLock (typically)
    public const uint Mod4Mask = 1 << 6;   // Super / Meta (typically)

    /// <summary>Grab a key on <paramref name="grabWindow"/> (use the root for a global hotkey).</summary>
    [DllImport(X11)]
    public static extern int XGrabKey(nint display, int keycode, uint modifiers, nint grabWindow,
        bool ownerEvents, int pointerMode, int keyboardMode);

    [DllImport(X11)]
    public static extern int XUngrabKey(nint display, int keycode, uint modifiers, nint grabWindow);

    /// <summary>Map a keysym to the keycode on the server's current layout (0 if unmapped).</summary>
    [DllImport(X11)]
    public static extern byte XKeysymToKeycode(nint display, nuint keysym);

    /// <summary>Block for the next event, copying it into <paramref name="ev"/>.</summary>
    [DllImport(X11)]
    public static extern int XNextEvent(nint display, ref XEvent ev);

    /// <summary>Number of events already queued (lets us drain without blocking).</summary>
    [DllImport(X11)]
    public static extern int XPending(nint display);

    [DllImport(X11)]
    public static extern int XSync(nint display, bool discard);

    /// <summary>
    /// Enough of Xlib's <c>XEvent</c> union (192 bytes on LP64) to read a key-press. Only the
    /// fields we use are declared, at their <c>XKeyEvent</c> offsets; the explicit size makes
    /// <see cref="XNextEvent"/>'s full-union write land inside the struct.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 192)]
    public struct XEvent
    {
        [FieldOffset(0)] public int type;
        [FieldOffset(80)] public uint state;    // XKeyEvent.state  (modifier mask at the press)
        [FieldOffset(84)] public uint keycode;  // XKeyEvent.keycode
    }

    // ---- libXrandr ----------------------------------------------------------

    /// <summary>Cached resources (no hardware poll). Returns <c>XRRScreenResources</c>* (NULL if RandR is absent).</summary>
    [DllImport(Xrandr)]
    public static extern nint XRRGetScreenResourcesCurrent(nint display, nint window);

    [DllImport(Xrandr)]
    public static extern void XRRFreeScreenResources(nint resources);

    /// <summary>Geometry for one CRTC. Returns <c>XRRCrtcInfo</c>* (free with <see cref="XRRFreeCrtcInfo"/>).</summary>
    [DllImport(Xrandr)]
    public static extern nint XRRGetCrtcInfo(nint display, nint resources, nint crtc);

    [DllImport(Xrandr)]
    public static extern void XRRFreeCrtcInfo(nint crtcInfo);

    // ---- structs (LP64; only the leading fields we read are declared) -------

    /// <summary>
    /// Leading fields of Xlib's <c>XImage</c>, through the colour masks. Trailing members
    /// (<c>obdata</c>, the <c>funcs</c> table) are intentionally omitted — we never call the
    /// embedded function pointers; cleanup frees <see cref="data"/> and the struct via
    /// <see cref="XFree"/>, which is what <c>XDestroyImage</c> does for an <c>XGetImage</c> result.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct XImage
    {
        public int width;
        public int height;
        public int xoffset;
        public int format;
        public nint data;            // char*  — the pixel buffer
        public int byte_order;       // LSBFirst / MSBFirst
        public int bitmap_unit;
        public int bitmap_bit_order;
        public int bitmap_pad;
        public int depth;
        public int bytes_per_line;   // row stride in bytes (>= width * bits_per_pixel/8)
        public int bits_per_pixel;   // 16 / 24 / 32
        public nuint red_mask;       // unsigned long
        public nuint green_mask;
        public nuint blue_mask;
    }

    /// <summary>Leading fields of <c>XRRScreenResources</c>, through the CRTC list.</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct XRRScreenResources
    {
        public nint timestamp;       // Time
        public nint configTimestamp; // Time
        public int ncrtc;
        public nint crtcs;           // RRCrtc* (array of XID / unsigned long)
        public int noutput;
        public nint outputs;         // RROutput*
        public int nmode;
        public nint modes;           // XRRModeInfo*
    }

    /// <summary>Leading fields of <c>XRRCrtcInfo</c>, through <c>mode</c> (0 == this CRTC is disabled).</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct XRRCrtcInfo
    {
        public nint timestamp;       // Time
        public int x;
        public int y;
        public uint width;
        public uint height;
        public nint mode;            // RRMode (XID); 0 => output off
    }
}
