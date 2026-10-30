// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Shinra CC

using System.IO;
using Avalonia.Media.Imaging;
using ShinraShot.App.Imaging;
using ShinraShot.Core.Capture;
using ShinraShot.Core.Imaging;
using ShinraShot.Core.Platform;

namespace ShinraShot.App.Capture;

/// <summary>The outcome of a capture: the grab, a ready-to-show bitmap, and what was persisted.</summary>
public sealed record CaptureOutcome(CapturedImage Image, WriteableBitmap Bitmap, string? SavedPath, int PngByteCount);

/// <summary>
/// Orchestrates a capture: grab pixels (platform backend), turn them into a bitmap + PNG,
/// and apply the requested destinations (save, clipboard).
///
/// Threading: <see cref="Grab"/> / <see cref="GrabRegion"/> are pure Win32/X11 and may run on
/// a background thread. <see cref="Process"/> builds Avalonia bitmaps, so call it on the UI thread.
/// </summary>
public sealed class CaptureService
{
    private readonly ICaptureBackend _backend = PlatformServices.CreateCaptureBackend();
    private readonly IClipboardImage _clipboard = PlatformServices.CreateClipboardImage();

    public ICaptureBackend Backend => _backend;

    public CapturedImage Grab() => _backend.CaptureVirtualScreen();

    public CapturedImage GrabRegion(ScreenRect region) => _backend.Capture(region);

    /// <summary>Put an image (plus optional PNG bytes) on the clipboard.</summary>
    public void Copy(CapturedImage img, byte[] png) => _clipboard.SetImage(img, png);

    /// <summary>Auto-name a PNG into the default (or given) folder; returns the written path.</summary>
    public string SaveToFolder(byte[] png, string? directory = null)
    {
        directory ??= CaptureOptions.DefaultDirectory;
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, string.Format("ShinraShot_{0:yyyy-MM-dd_HH-mm-ss}.png", DateTime.Now));
        File.WriteAllBytes(path, png);
        return path;
    }

    public CaptureOutcome Process(CapturedImage img, CaptureOptions opts)
    {
        var bmp = AvaloniaImageInterop.ToWriteableBitmap(img);
        byte[] png = AvaloniaImageInterop.EncodePng(bmp);

        string? saved = null;
        if (opts.SaveToFolder)
        {
            Directory.CreateDirectory(opts.SaveDirectory);
            saved = Path.Combine(opts.SaveDirectory, opts.BuildFileName());
            File.WriteAllBytes(saved, png);
        }

        if (opts.CopyToClipboard)
            _clipboard.SetImage(img, png);

        return new CaptureOutcome(img, bmp, saved, png.Length);
    }

    /// <summary>Convenience: grab the whole virtual desktop and process it in one call.</summary>
    public CaptureOutcome CaptureFullScreen(CaptureOptions opts) => Process(Grab(), opts);
}
