// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Shinra CC

using ShinraShot.App.Editor;
using ShinraShot.App.Imaging;
using ShinraShot.App.Notifications;
using ShinraShot.Core.Capture;
using ShinraShot.Core.Imaging;

namespace ShinraShot.App.Capture;

/// <summary>
/// Turns a finished <see cref="CapturedImage"/> into bitmap + PNG once, then applies the
/// configured default action — or shows the action menu when the default is <see cref="DefaultCaptureAction.Ask"/>.
/// Must be called on the UI thread (it builds Avalonia bitmaps and may open windows).
/// </summary>
public sealed class CaptureFlow
{
    private readonly CaptureService _svc;

    public CaptureFlow(CaptureService svc) => _svc = svc;

    /// <summary>The default action to take, applied from persisted settings at startup.</summary>
    public DefaultCaptureAction DefaultAction { get; set; } = DefaultCaptureAction.Ask;

    /// <summary>Target folder for auto-save; <see langword="null"/> uses the built-in default.</summary>
    public string? SaveDirectory { get; set; }

    public void Complete(CapturedImage img)
    {
        var bmp = AvaloniaImageInterop.ToWriteableBitmap(img);
        byte[] png = AvaloniaImageInterop.EncodePng(bmp);

        switch (DefaultAction)
        {
            case DefaultCaptureAction.CopyToClipboard:
                _svc.Copy(img, png);
                ToastService.Show($"Captured {img.Width}×{img.Height} — copied to clipboard");
                break;

            case DefaultCaptureAction.SaveToFolder:
                var path = _svc.SaveToFolder(png, SaveDirectory);
                ToastService.Show($"Captured {img.Width}×{img.Height} — saved\n{System.IO.Path.GetFileName(path)}", path);
                break;

            case DefaultCaptureAction.OpenInEditor:
                OpenEditor(img, bmp, png);
                break;

            default:
                new CaptureActionWindow(img, bmp, png, _svc, OpenEditor).ShowMenu();
                break;
        }
    }

    private void OpenEditor(CapturedImage img, Avalonia.Media.Imaging.WriteableBitmap bmp, byte[] png)
    {
        var ed = new EditorWindow(img, bmp, png, _svc);
        ed.Show();
        ed.Activate();
    }
}
