// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Shinra CC

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using ShinraShot.App.Notifications;
using ShinraShot.App.Theming;
using ShinraShot.Core.Imaging;

namespace ShinraShot.App.Capture;

/// <summary>
/// The Greenshot-style post-capture chooser: a thumbnail of the grab plus the destinations
/// — Open in Editor, Copy, Save, Save As…, Discard. (A configurable default action that skips
/// this menu lands with Settings in slice 5.)
/// </summary>
public sealed class CaptureActionWindow : Window
{
    private readonly CapturedImage _img;
    private readonly WriteableBitmap _bmp;
    private readonly byte[] _png;
    private readonly CaptureService _svc;
    private readonly Action<CapturedImage, WriteableBitmap, byte[]> _openEditor;

    public CaptureActionWindow(CapturedImage img, WriteableBitmap bmp, byte[] png,
                               CaptureService svc, Action<CapturedImage, WriteableBitmap, byte[]> openEditor)
    {
        _img = img;
        _bmp = bmp;
        _png = png;
        _svc = svc;
        _openEditor = openEditor;

        Title = "ShinraShot";
        SizeToContent = SizeToContent.WidthAndHeight;
        CanResize = false;
        Topmost = true;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        TrySetIcon();

        var header = new TextBlock
        {
            Text = $"Capture ready — {img.Width}×{img.Height}",
            FontSize = 15,
            FontWeight = FontWeight.SemiBold,
            Margin = new Thickness(0, 0, 0, 10),
        };

        var thumb = new Image
        {
            Source = bmp,
            MaxWidth = 460,
            MaxHeight = 300,
            Stretch = Stretch.Uniform,
        };
        var thumbBorder = new Border
        {
            Child = thumb,
            BorderBrush = Palette.BorderBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(2),
            Background = Palette.CanvasBrush,
            HorizontalAlignment = HorizontalAlignment.Center,
        };

        var buttons = new WrapPanel { Margin = new Thickness(0, 14, 0, 0), HorizontalAlignment = HorizontalAlignment.Center };
        buttons.Children.Add(PrimaryButton("Open in Editor", OpenEditor));
        buttons.Children.Add(Button("Copy", Copy));
        buttons.Children.Add(Button("Save", Save));
        buttons.Children.Add(Button("Save As…", SaveAs));
        buttons.Children.Add(Button("Discard", Close));

        Content = new StackPanel
        {
            Margin = new Thickness(18),
            Children = { header, thumbBorder, buttons },
        };
    }

    public void ShowMenu()
    {
        Show();
        Activate();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        switch (e.Key)
        {
            case Key.Escape: Close(); break;
            case Key.Enter or Key.Return: OpenEditor(); break;
            case Key.C: Copy(); break;
            case Key.S: Save(); break;
        }
    }

    private void OpenEditor()
    {
        _openEditor(_img, _bmp, _png);
        Close();
    }

    private void Copy()
    {
        _svc.Copy(_img, _png);
        ToastService.Show("Copied to clipboard");
        Close();
    }

    private void Save()
    {
        var p = _svc.SaveToFolder(_png);
        ToastService.Show("Saved\n" + System.IO.Path.GetFileName(p), p);
        Close();
    }

    private async void SaveAs()
    {
        var p = await ImageSink.SaveAsAsync(this, _png);
        if (p is not null) ToastService.Show("Saved\n" + System.IO.Path.GetFileName(p), p);
        Close();
    }

    private static Button Button(string text, Action onClick)
    {
        var b = new Button { Content = text, Padding = new Thickness(14, 8), Margin = new Thickness(4) };
        b.Click += (_, _) => onClick();
        return b;
    }

    private static Button PrimaryButton(string text, Action onClick)
    {
        var b = Button(text, onClick);
        b.Classes.Add("accent");
        return b;
    }

    private void TrySetIcon()
    {
        try { Icon = new WindowIcon(new Bitmap(AssetLoader.Open(new Uri("avares://ShinraShot/Assets/shinrashot.png")))); }
        catch { /* ignore */ }
    }
}
