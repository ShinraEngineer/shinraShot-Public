// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Shinra CC

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using ShinraShot.App.Capture;
using ShinraShot.App.Theming;
using ShinraShot.Core.Capture;
using ShinraShot.Core.Settings;

namespace ShinraShot.App.Settings;

/// <summary>
/// The ShinraShot settings window: default capture action, save folder, and the global
/// capture hotkeys. Edits a cloned draft and only calls back on Save, so Cancel discards.
/// </summary>
public sealed class SettingsWindow : Window
{
    private static readonly (DefaultCaptureAction Value, string Label)[] s_actions =
    {
        (DefaultCaptureAction.Ask, "Show the action menu"),
        (DefaultCaptureAction.OpenInEditor, "Open in the editor"),
        (DefaultCaptureAction.CopyToClipboard, "Copy to clipboard"),
        (DefaultCaptureAction.SaveToFolder, "Save to folder"),
    };

    private readonly AppSettings _draft;
    private readonly Action<AppSettings> _onSaved;

    private readonly ComboBox _actionCombo = new() { HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly TextBox _saveDirBox = new() { PlaceholderText = CaptureOptions.DefaultDirectory };
    private readonly CheckBox _hotkeysEnabled = new() { Content = "Enable global capture hotkeys" };
    private readonly HotkeyBox _regionBox = new();
    private readonly HotkeyBox _fullBox = new();

    public SettingsWindow(AppSettings current, Action<AppSettings> onSaved, bool checkUpdatesOnOpen = false)
    {
        _draft = current.Clone();
        _onSaved = onSaved;

        Title = "ShinraShot Settings";
        SizeToContent = SizeToContent.Height;
        Width = 440;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        TrySetIcon();

        // --- Default action ---
        foreach (var (value, label) in s_actions)
            _actionCombo.Items.Add(new ComboBoxItem { Content = label, Tag = value });
        _actionCombo.SelectedIndex = Math.Max(0, Array.FindIndex(s_actions, a => a.Value == _draft.DefaultAction));

        // --- Save folder ---
        _saveDirBox.Text = _draft.SaveDirectory ?? string.Empty;
        var browse = Button("Browse…", BrowseFolder);
        var saveDirRow = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        Grid.SetColumn(_saveDirBox, 0);
        Grid.SetColumn(browse, 1);
        browse.Margin = new Thickness(8, 0, 0, 0);
        saveDirRow.Children.Add(_saveDirBox);
        saveDirRow.Children.Add(browse);

        // --- Hotkeys ---
        _hotkeysEnabled.IsChecked = _draft.GlobalHotkeysEnabled;
        _hotkeysEnabled.IsCheckedChanged += (_, _) => UpdateHotkeyEnabled();
        _regionBox.Value = _draft.RegionHotkey;
        _fullBox.Value = _draft.FullScreenHotkey;
        UpdateHotkeyEnabled();

        // --- Buttons ---
        var save = PrimaryButton("Save", SaveAndClose);
        var cancel = Button("Cancel", Close);
        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8,
            Margin = new Thickness(0, 18, 0, 0),
            Children = { cancel, save },
        };

        var form = new StackPanel
        {
            Margin = new Thickness(20),
            Spacing = 6,
            Children =
            {
                Caption("When I take a capture"),
                _actionCombo,
                Caption("Save captures to", top: 14),
                saveDirRow,
                Hint("Leave empty to use the default folder shown above."),
                Caption("Hotkeys", top: 14),
                _hotkeysEnabled,
                HotkeyRow("Region capture", _regionBox),
                HotkeyRow("Full-screen capture", _fullBox),
                Hint("Click a field and press the keys. Backspace clears it."),
                Caption("Updates", top: 14),
                new UpdateSection(checkUpdatesOnOpen),
                buttons,
            },
        };

        Content = form;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (!e.Handled && e.Key == Key.Escape)
            Close();
    }

    private void UpdateHotkeyEnabled()
    {
        bool on = _hotkeysEnabled.IsChecked == true;
        _regionBox.IsEnabled = on;
        _fullBox.IsEnabled = on;
        _regionBox.Opacity = on ? 1.0 : 0.5;
        _fullBox.Opacity = on ? 1.0 : 0.5;
    }

    private async void BrowseFolder()
    {
        var picked = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Choose the save folder",
            AllowMultiple = false,
        });

        if (picked.Count > 0 && picked[0].TryGetLocalPath() is { } path)
            _saveDirBox.Text = path;
    }

    private void SaveAndClose()
    {
        _draft.DefaultAction = (DefaultCaptureAction)((ComboBoxItem)_actionCombo.SelectedItem!).Tag!;
        _draft.SaveDirectory = string.IsNullOrWhiteSpace(_saveDirBox.Text) ? null : _saveDirBox.Text!.Trim();
        _draft.GlobalHotkeysEnabled = _hotkeysEnabled.IsChecked == true;
        _draft.RegionHotkey = _regionBox.Value;
        _draft.FullScreenHotkey = _fullBox.Value;

        _onSaved(_draft);
        Close();
    }

    // --- small UI builders, matching the code-built style used elsewhere ---

    private static TextBlock Caption(string text, double top = 0) => new()
    {
        Text = text,
        FontWeight = FontWeight.SemiBold,
        Foreground = Palette.TextBrush,
        Margin = new Thickness(0, top, 0, 2),
    };

    private static TextBlock Hint(string text) => new()
    {
        Text = text,
        FontSize = 12,
        Foreground = Palette.SecondaryBrush,
        TextWrapping = TextWrapping.Wrap,
    };

    private static Control HotkeyRow(string label, HotkeyBox box)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Margin = new Thickness(0, 4, 0, 0) };
        var caption = new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center, Foreground = Palette.TextBrush };
        Grid.SetColumn(caption, 0);
        Grid.SetColumn(box, 1);
        grid.Children.Add(caption);
        grid.Children.Add(box);
        return grid;
    }

    private static Button Button(string text, Action onClick)
    {
        var b = new Button { Content = text, Padding = new Thickness(16, 8) };
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
