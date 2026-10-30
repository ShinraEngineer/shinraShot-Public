// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Shinra CC

using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using ShinraShot.App.Theming;
using ShinraShot.Core;
using ShinraShot.Core.Update;

namespace ShinraShot.App.Settings;

/// <summary>
/// The "Updates" block of the settings window: shows the current version, checks the published
/// <c>update.json</c> for a newer build, and (when one exists) offers a download with the shared
/// download icon, a progress bar, and a button to open the downloaded installer. The check and
/// download run against <see cref="UpdateService"/>; no install happens automatically.
/// </summary>
internal sealed class UpdateSection : StackPanel
{
    private readonly UpdateService _service;

    private readonly TextBlock _status = new()
    {
        Foreground = Palette.SecondaryBrush,
        TextWrapping = TextWrapping.Wrap,
        VerticalAlignment = VerticalAlignment.Center,
    };

    private readonly Button _checkButton;
    private readonly TextBlock _notes = new()
    {
        Foreground = Palette.SecondaryBrush,
        FontSize = 12,
        TextWrapping = TextWrapping.Wrap,
        Margin = new Thickness(0, 4, 0, 0),
    };

    private readonly Button _downloadButton;
    private readonly Image _readyIcon;
    private readonly StackPanel _downloadRow;
    private readonly ProgressBar _progress = new() { Minimum = 0, Maximum = 1, Height = 6, IsVisible = false, Margin = new Thickness(0, 8, 0, 0) };
    private readonly StackPanel _openRow;
    private readonly Button _openButton;
    private readonly Button _folderButton;

    private UpdateCheckResult? _result;
    private string? _downloadedPath;
    private bool _busy;

    public UpdateSection(bool autoCheck = false)
    {
        _service = new UpdateService();
        Spacing = 4;

        _status.Text = $"{AppInfo.Name} {AppInfo.Version}";

        _checkButton = new Button { Content = "Check for updates", Padding = new Thickness(14, 7) };
        _checkButton.Click += (_, _) => _ = RunCheckAsync();

        var topRow = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        Grid.SetColumn(_status, 0);
        Grid.SetColumn(_checkButton, 1);
        topRow.Children.Add(_status);
        topRow.Children.Add(_checkButton);

        _downloadButton = BuildDownloadButton();
        _downloadButton.Click += (_, _) => _ = RunDownloadAsync();

        // The Shinra "UPD RDY" panel is the update-available indicator: it sits beside the
        // download action and is shown only once a newer build has been found.
        _readyIcon = new Image
        {
            Width = 48,
            Height = 48,
            Source = AppImages.UpdateReady,
            VerticalAlignment = VerticalAlignment.Center,
        };
        _downloadRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 12,
            Margin = new Thickness(0, 8, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
            IsVisible = false,
            Children = { _readyIcon, _downloadButton },
        };

        _openButton = new Button { Content = "Open installer", Padding = new Thickness(14, 7) };
        _openButton.Classes.Add("accent");
        _openButton.Click += (_, _) => OpenPath(_downloadedPath, reveal: false);
        _folderButton = new Button { Content = "Show in folder", Padding = new Thickness(14, 7) };
        _folderButton.Click += (_, _) => OpenPath(_downloadedPath, reveal: true);
        _openRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Margin = new Thickness(0, 8, 0, 0),
            IsVisible = false,
            Children = { _openButton, _folderButton },
        };

        Children.Add(topRow);
        Children.Add(_notes);
        Children.Add(_downloadRow);
        Children.Add(_progress);
        Children.Add(_openRow);

        _notes.IsVisible = false;

        if (autoCheck)
            _ = RunCheckAsync();
    }

    private static Button BuildDownloadButton()
    {
        var button = new Button
        {
            Content = "Download update",
            Padding = new Thickness(14, 7),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
        };
        button.Classes.Add("accent");
        return button;
    }

    private async Task RunCheckAsync()
    {
        if (_busy) return;
        _busy = true;

        _checkButton.IsEnabled = false;
        _notes.IsVisible = false;
        _downloadRow.IsVisible = false;
        _openRow.IsVisible = false;
        _progress.IsVisible = false;
        SetStatus("Checking for updates...", Palette.SecondaryBrush);

        UpdateCheckResult result;
        try
        {
            result = await _service.CheckAsync();
        }
        catch (Exception ex)
        {
            result = UpdateCheckResult.Failed(AppInfo.Version, ex.Message);
        }

        _result = result;
        _checkButton.IsEnabled = true;
        _checkButton.Content = "Check again";

        if (!result.Success)
        {
            SetStatus(result.Message ?? "The update check failed.", Palette.AccentBrush);
        }
        else if (result.UpdateAvailable)
        {
            SetStatus($"Version {result.LatestVersion} is available. You have {result.CurrentVersion}.", Palette.TextBrush);
            if (!string.IsNullOrWhiteSpace(result.Notes))
            {
                _notes.Text = result.Notes;
                _notes.IsVisible = true;
            }
            _downloadButton.IsEnabled = true;
            _downloadRow.IsVisible = true;
        }
        else
        {
            string detail = result.Message is null ? "" : " " + result.Message;
            SetStatus($"You are on the latest version ({result.CurrentVersion})." + detail, Palette.SecondaryBrush);
        }

        _busy = false;
    }

    private async Task RunDownloadAsync()
    {
        if (_busy || _result is not { UpdateAvailable: true } result)
            return;
        _busy = true;

        _downloadButton.IsEnabled = false;
        _checkButton.IsEnabled = false;
        _progress.Value = 0;
        _progress.IsVisible = true;
        SetStatus($"Downloading {result.DownloadFileName}...", Palette.TextBrush);

        var progress = new Progress<double>(p => Dispatcher.UIThread.Post(() => _progress.Value = p));
        try
        {
            string dir = DownloadDirectory();
            _downloadedPath = await _service.DownloadAsync(result, dir, progress);

            _progress.Value = 1;
            _progress.IsVisible = false;
            _downloadRow.IsVisible = false;
            SetStatus($"Downloaded to {_downloadedPath}", Palette.TextBrush);
            _openRow.IsVisible = true;
        }
        catch (Exception ex)
        {
            _progress.IsVisible = false;
            _downloadButton.IsEnabled = true;
            SetStatus("Download failed. " + ex.Message, Palette.AccentBrush);
        }

        _checkButton.IsEnabled = true;
        _busy = false;
    }

    private void SetStatus(string text, IBrush brush)
    {
        _status.Text = text;
        _status.Foreground = brush;
    }

    private static string DownloadDirectory()
    {
        try
        {
            string downloads = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
            if (Directory.Exists(downloads))
                return downloads;
        }
        catch { /* fall through */ }
        return Path.Combine(Path.GetTempPath(), "ShinraShot-updates");
    }

    private static void OpenPath(string? path, bool reveal)
    {
        if (string.IsNullOrEmpty(path))
            return;

        try
        {
            if (OperatingSystem.IsWindows())
            {
                if (reveal)
                    Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{path}\"") { UseShellExecute = true });
                else
                    Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            }
            else if (OperatingSystem.IsLinux())
            {
                string target = reveal ? (Path.GetDirectoryName(path) ?? path) : path;
                Process.Start(new ProcessStartInfo("xdg-open", $"\"{target}\"") { UseShellExecute = true });
            }
        }
        catch { /* opening is best-effort */ }
    }
}
