// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Shinra CC

namespace ShinraShot.Core.Platform.Linux;

/// <summary>The display server a Linux session is running under.</summary>
public enum LinuxDisplayServer
{
    /// <summary>Could not be determined from the environment.</summary>
    Unknown = 0,

    /// <summary>A native X11 session.</summary>
    X11,

    /// <summary>A native Wayland session. X11 clients still work here through XWayland.</summary>
    Wayland,
}

/// <summary>
/// Best-effort detection of the Linux display server from the session environment.
/// This is advisory only: the X11 capture backend also serves Wayland sessions through
/// XWayland, so a <see cref="LinuxDisplayServer.Wayland"/> result is used to prefer a
/// native portal backend <em>when one is registered</em>, not as a hard requirement.
/// </summary>
public static class LinuxSession
{
    /// <summary>Classify the current session as X11, Wayland, or Unknown.</summary>
    public static LinuxDisplayServer DetectDisplayServer()
    {
        // XDG_SESSION_TYPE is the authoritative signal when the session/login manager sets it.
        string? sessionType = Environment.GetEnvironmentVariable("XDG_SESSION_TYPE");
        if (string.Equals(sessionType, "wayland", StringComparison.OrdinalIgnoreCase))
            return LinuxDisplayServer.Wayland;
        if (string.Equals(sessionType, "x11", StringComparison.OrdinalIgnoreCase))
            return LinuxDisplayServer.X11;

        // Otherwise fall back to the presence of each server's socket environment variable.
        // WAYLAND_DISPLAY wins: under a Wayland session DISPLAY is usually also set (XWayland).
        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WAYLAND_DISPLAY")))
            return LinuxDisplayServer.Wayland;
        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DISPLAY")))
            return LinuxDisplayServer.X11;

        return LinuxDisplayServer.Unknown;
    }
}
