// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Shinra CC

using System.Runtime.Versioning;
using ShinraShot.Core.Capture;
using ShinraShot.Core.Input;
using ShinraShot.Core.Platform.Linux;
using ShinraShot.Core.Platform.Windows;

namespace ShinraShot.Core.Platform;

/// <summary>
/// Runtime selection of the platform-specific capture and clipboard backends.
/// The UI layer talks only to the interfaces; this is the one place that knows
/// which OS it is on.
/// </summary>
public static class PlatformServices
{
    // Supplied by the UI layer at startup (see RegisterLinuxWaylandBackend). The native
    // Wayland capture path — org.freedesktop.portal.Screenshot over D-Bus — needs a D-Bus
    // client and a PNG decoder, both of which live in the App layer (via Avalonia) rather
    // than in this pure-Core assembly. So rather than Core constructing it, the App pushes a
    // factory in. Null until something registers one; see the App's LinuxWaylandCapture.
    private static Func<ICaptureBackend>? s_linuxWaylandBackendFactory;

    /// <summary>
    /// Register a native-Wayland capture backend supplied by the UI layer. Once registered,
    /// <see cref="CreateCaptureBackend"/> prefers it over the X11/XWayland backend on sessions
    /// detected as Wayland (see <see cref="LinuxSession"/>). Until something registers one,
    /// Wayland sessions keep capturing through XWayland. This is the extension point for a
    /// future portal backend; nothing is wired to it yet.
    /// </summary>
    public static void RegisterLinuxWaylandBackend(Func<ICaptureBackend> factory)
        => s_linuxWaylandBackendFactory = factory ?? throw new ArgumentNullException(nameof(factory));

    public static ICaptureBackend CreateCaptureBackend()
    {
        if (OperatingSystem.IsWindows())
            return new WindowsCaptureBackend();
        if (OperatingSystem.IsLinux())
            return CreateLinuxCaptureBackend();
        throw new PlatformNotSupportedException("ShinraShot supports Windows and Linux.");
    }

    [SupportedOSPlatform("linux")]
    private static ICaptureBackend CreateLinuxCaptureBackend()
    {
        // Prefer a registered native-Wayland backend only on an actual Wayland session.
        // Everything else — native X11, or a Wayland session with no portal backend
        // registered — goes through the X11 backend, which also serves Wayland via XWayland.
        if (s_linuxWaylandBackendFactory is { } factory &&
            LinuxSession.DetectDisplayServer() == LinuxDisplayServer.Wayland)
        {
            return factory();
        }

        return new LinuxCaptureBackend();
    }

    public static IClipboardImage CreateClipboardImage()
    {
        if (OperatingSystem.IsWindows())
            return new WindowsClipboardImage();
        if (OperatingSystem.IsLinux())
            return new LinuxClipboardImage();
        throw new PlatformNotSupportedException("ShinraShot supports Windows and Linux.");
    }

    /// <summary>
    /// The OS-wide hotkey backend for this platform. Windows uses <c>RegisterHotKey</c>; Linux
    /// uses X11 <c>XGrabKey</c> (which also covers XWayland — a pure Wayland session needs the
    /// GlobalShortcuts portal, not wired yet). Any other host gets a no-op sink.
    /// </summary>
    public static IGlobalHotkeys CreateGlobalHotkeys()
    {
        if (OperatingSystem.IsWindows())
            return new WindowsGlobalHotkeys();
        if (OperatingSystem.IsLinux())
            return new LinuxGlobalHotkeys();
        return new NullGlobalHotkeys();
    }
}
