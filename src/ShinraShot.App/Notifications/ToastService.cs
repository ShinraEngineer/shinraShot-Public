// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Shinra CC

using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using ShinraShot.App.Theming;

namespace ShinraShot.App.Notifications;

/// <summary>
/// A tiny, dependency-free dark toast shown bottom-right. Used for capture confirmations
/// until the richer post-capture action menu arrives (slice 3). Click to open the saved
/// file's folder; auto-dismisses after a few seconds. Any failure here is swallowed so it
/// can never break a capture.
/// </summary>
public static class ToastService
{
    private static readonly Lazy<Bitmap?> Logo = new(() =>
    {
        try { return new Bitmap(AssetLoader.Open(new Uri("avares://ShinraShot/Assets/shinrashot.png"))); }
        catch { return null; }
    });

    public static void Show(string message, string? openFolderForPath = null, Action? onClick = null, Bitmap? icon = null)
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(() => Show(message, openFolderForPath, onClick, icon));
            return;
        }

        try { ShowCore(message, openFolderForPath, onClick, icon); }
        catch { /* a toast must never take the app down */ }
    }

    private static void ShowCore(string message, string? openFolderForPath, Action? onClick, Bitmap? icon)
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        // The caller may supply its own art (the "UPD RDY" panel on an update toast); otherwise
        // fall back to the ShinraShot logo.
        if ((icon ?? Logo.Value) is { } art)
            panel.Children.Add(new Image { Width = 28, Height = 28, Source = art, VerticalAlignment = VerticalAlignment.Center });
        panel.Children.Add(new TextBlock
        {
            Text = message,
            Foreground = Brushes.White,
            FontSize = 13,
            MaxWidth = 340,
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center,
        });

        var border = new Border
        {
            Background = Palette.CardBrush,
            BorderBrush = Palette.AccentBrush,
            BorderThickness = new Thickness(0, 2, 0, 0),
            Padding = new Thickness(14, 12),
            Child = panel,
        };

        var win = new Window
        {
            WindowDecorations = WindowDecorations.None,
            CanResize = false,
            ShowInTaskbar = false,
            Topmost = true,
            ShowActivated = false,
            SizeToContent = SizeToContent.WidthAndHeight,
            Background = Brushes.Transparent,
            Content = border,
        };

        win.PointerPressed += (_, _) =>
        {
            if (onClick is not null)
                try { onClick(); } catch { /* a toast click must never take the app down */ }
            else if (!string.IsNullOrEmpty(openFolderForPath))
                OpenInFolder(openFolderForPath);
            SafeClose(win);
        };
        win.Opened += (_, _) => Reposition(win);
        win.Show();

        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3.5) };
        timer.Tick += (_, _) => { timer.Stop(); SafeClose(win); };
        timer.Start();
    }

    private static void Reposition(Window win)
    {
        try
        {
            var screen = win.Screens.ScreenFromWindow(win) ?? win.Screens.Primary ?? win.Screens.All[0];
            var wa = screen.WorkingArea;                    // physical pixels
            double scale = screen.Scaling;
            int w = (int)(win.ClientSize.Width * scale);
            int h = (int)(win.ClientSize.Height * scale);
            int margin = (int)(24 * scale);
            win.Position = new PixelPoint(wa.X + wa.Width - w - margin, wa.Y + wa.Height - h - margin);
        }
        catch { /* leave at default position */ }
    }

    private static void OpenInFolder(string path)
    {
        try
        {
            if (OperatingSystem.IsWindows())
                Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{path}\"") { UseShellExecute = true });
            else if (OperatingSystem.IsLinux())
                Process.Start(new ProcessStartInfo("xdg-open", $"\"{System.IO.Path.GetDirectoryName(path)}\"") { UseShellExecute = true });
        }
        catch { /* ignore */ }
    }

    private static void SafeClose(Window win)
    {
        try { win.Close(); } catch { /* ignore */ }
    }
}
