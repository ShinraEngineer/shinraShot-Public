// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Shinra CC

using System.Runtime.Versioning;
using ShinraShot.Core.Capture;
using ShinraShot.Core.Imaging;
using ShinraShot.Core.Platform;
using ShinraShot.Core.Platform.Linux;

namespace ShinraShot.App.Platform.Linux;

/// <summary>
/// Hook-up point for native Wayland screen capture. Deliberately inert today.
///
/// Wayland's screenshot story — and third-party app support generally — is still patchy,
/// so ShinraShot runs on Wayland sessions through XWayland (the Core X11 backend) for now.
/// The seam exists so a native portal backend can be dropped in later without touching the
/// capture / overlay / editor pipeline: implement <see cref="WaylandScreenshotBackend"/> and
/// have <see cref="TryRegister"/> register it. Nothing else has to change.
/// </summary>
internal static class LinuxWaylandCapture
{
    /// <summary>
    /// Called once at startup on Linux (from <c>Program.Main</c>), before any capture backend
    /// is created. When a native Wayland backend is implemented, this is where it registers
    /// itself with Core:
    /// <code>
    /// if (LinuxSession.DetectDisplayServer() == LinuxDisplayServer.Wayland &amp;&amp; PortalAvailable())
    ///     PlatformServices.RegisterLinuxWaylandBackend(() => new WaylandScreenshotBackend());
    /// </code>
    /// Until then it does nothing and Wayland sessions keep using XWayland. The reference to
    /// <see cref="LinuxSession"/> and <see cref="PlatformServices"/> here keeps the wire warm:
    /// the only remaining work to go native is filling in the backend and the two lines above.
    /// </summary>
    [SupportedOSPlatform("linux")]
    public static void TryRegister()
    {
        // Intentionally a no-op. See WaylandScreenshotBackend for the implementation plan.
        // Do NOT register the stub backend: it throws, and registering it would break the
        // working XWayland fallback on Wayland sessions.
    }
}

/// <summary>
/// Native Wayland capture via the <c>org.freedesktop.portal.Screenshot</c> desktop portal
/// (D-Bus). NOT IMPLEMENTED — this type only fixes the shape the implementation will take so
/// wiring it in is a drop-in. It is never constructed while
/// <see cref="LinuxWaylandCapture.TryRegister"/> stays a no-op.
///
/// Implementation plan (App layer, reusing Avalonia's transitive dependencies):
///   1. Call <c>org.freedesktop.portal.Screenshot.Screenshot</c> over D-Bus using
///      Tmds.DBus.Protocol (ships transitively with Avalonia's Linux backend).
///   2. The portal returns, on its Response signal, a file URI to a PNG it wrote.
///   3. Decode that PNG with Avalonia/Skia (Core has no image decoder) into top-down
///      BGRA8888 and wrap it in a <see cref="CapturedImage"/>, matching the X11 backend.
///   4. Portal capture is whole-screen / interactive; region selection stays in the existing
///      overlay, cropping the returned frame — no portal-specific UI needed.
///
/// Cannot be validated under WSLg (no Screenshot portal); needs a real GNOME/KDE box.
/// </summary>
[SupportedOSPlatform("linux")]
internal sealed class WaylandScreenshotBackend : ICaptureBackend
{
    private const string NotImplemented =
        "Native Wayland portal capture is not implemented yet; " +
        "ShinraShot captures through XWayland on Wayland sessions.";

    public ScreenRect VirtualBounds => throw new NotSupportedException(NotImplemented);

    public IReadOnlyList<ScreenInfo> GetScreens() => throw new NotSupportedException(NotImplemented);

    public CapturedImage Capture(ScreenRect region) => throw new NotSupportedException(NotImplemented);
}
