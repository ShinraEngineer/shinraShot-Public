// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Shinra CC

using ShinraShot.Core.Imaging;

namespace ShinraShot.Core.Capture;

/// <summary>
/// Grabs pixels from the screen. One implementation per platform
/// (Win32 GDI/DXGI, X11, Wayland portal). All coordinates are virtual-desktop
/// physical pixels, matching <see cref="VirtualBounds"/>.
/// </summary>
public interface ICaptureBackend
{
    /// <summary>The union of all displays, in physical pixels.</summary>
    ScreenRect VirtualBounds { get; }

    /// <summary>All connected displays.</summary>
    IReadOnlyList<ScreenInfo> GetScreens();

    /// <summary>Grab an arbitrary rectangle of the virtual desktop.</summary>
    CapturedImage Capture(ScreenRect region);

    /// <summary>Grab the entire virtual desktop (all monitors).</summary>
    CapturedImage CaptureVirtualScreen() => Capture(VirtualBounds);
}
