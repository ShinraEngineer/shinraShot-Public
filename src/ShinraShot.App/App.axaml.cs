// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Shinra CC

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using ShinraShot.App.Capture;
using ShinraShot.App.Imaging;
using ShinraShot.App.Notifications;
using ShinraShot.App.Overlay;
using ShinraShot.App.Settings;
using ShinraShot.Core;
using ShinraShot.Core.Capture;
using ShinraShot.Core.Input;
using ShinraShot.Core.Platform;
using ShinraShot.Core.Settings;
using ShinraShot.Core.Update;

namespace ShinraShot.App;

public partial class App : Application
{
    private CaptureService? _capture;
    private CaptureService Capture => _capture ??= new CaptureService();

    private readonly SettingsStore _settingsStore = new();
    private AppSettings? _settings;

    /// <summary>Persisted settings, loaded once and reused across the app's lifetime.</summary>
    private AppSettings Settings => _settings ??= _settingsStore.Load();

    private CaptureFlow? _flow;
    private CaptureFlow Flow => _flow ??= new CaptureFlow(Capture)
    {
        DefaultAction = Settings.DefaultAction,
        SaveDirectory = Settings.SaveDirectory,
    };

    // Action ids shared between hotkey registration and the Pressed dispatch.
    private const string RegionAction = "region";
    private const string FullScreenAction = "fullscreen";

    private IGlobalHotkeys? _hotkeys;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // ShinraShot lives in the tray with no window open at rest, so the app
            // must not exit just because its last window closed.
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
        }

        // The hotkey window must be created on the UI thread so its WM_HOTKEY messages are
        // delivered by this thread's message loop.
        _hotkeys = PlatformServices.CreateGlobalHotkeys();
        _hotkeys.Pressed += OnHotkeyPressed;
        RegisterHotkeys(Settings);

        // A quiet background check on launch: surface a toast only when an update is waiting.
        _ = CheckForUpdatesInBackgroundAsync();

        base.OnFrameworkInitializationCompleted();
    }

    private async Task CheckForUpdatesInBackgroundAsync()
    {
        try
        {
            var result = await new UpdateService().CheckAsync();
            if (result.UpdateAvailable)
                ToastService.Show(
                    $"{AppInfo.Name} {result.LatestVersion} is available. Click to update.",
                    onClick: () => OpenSettings(checkUpdates: true));
        }
        catch
        {
            // An update check must never disturb startup; stay silent on any failure.
        }
    }

    // --- Tray menu handlers (wired up feature-by-feature in later slices) ---

    private async void OnCaptureRegion(object? sender, EventArgs e)
    {
        try
        {
            var frozen = await Task.Run(() => Capture.Grab());
            var bmp = AvaloniaImageInterop.ToWriteableBitmap(frozen);
            var backend = Capture.Backend;
            double scale = PrimaryScale(backend);

            var win = new RegionOverlayWindow(frozen, bmp, backend.VirtualBounds, scale);
            var sel = await win.PickAsync();
            if (sel is null) return;   // cancelled

            var cropped = frozen.Crop(sel.Value.X, sel.Value.Y, sel.Value.Width, sel.Value.Height);
            Flow.Complete(cropped);
        }
        catch (Exception ex)
        {
            ToastService.Show("Region capture failed: " + ex.Message);
        }
    }

    private static double PrimaryScale(ICaptureBackend backend)
    {
        try
        {
            foreach (var s in backend.GetScreens())
                if (s.IsPrimary && s.Scale > 0) return s.Scale;
        }
        catch { /* fall through */ }
        return 1.0;
    }

    private async void OnCaptureFullScreen(object? sender, EventArgs e)
    {
        try
        {
            // GDI grab off the UI thread; everything after (bitmap/encode/menu) on it.
            var img = await Task.Run(() => Capture.Grab());
            Flow.Complete(img);
        }
        catch (Exception ex)
        {
            ToastService.Show("Capture failed: " + ex.Message);
        }
    }

    private SettingsWindow? _settingsWindow;

    private void OnSettings(object? sender, EventArgs e) => OpenSettings(checkUpdates: false);

    private void OnCheckForUpdates(object? sender, EventArgs e) => OpenSettings(checkUpdates: true);

    private void OpenSettings(bool checkUpdates)
    {
        // Only one settings window at a time; re-focus it if it is already open.
        if (_settingsWindow is not null)
        {
            _settingsWindow.Activate();
            return;
        }

        _settingsWindow = new SettingsWindow(Settings, ApplySettings, checkUpdates);
        _settingsWindow.Closed += (_, _) => _settingsWindow = null;
        _settingsWindow.Show();
        _settingsWindow.Activate();
    }

    /// <summary>Persist the edited settings and apply them to the running app.</summary>
    private void ApplySettings(AppSettings saved)
    {
        _settingsStore.Save(saved);
        _settings = saved;

        if (_flow is not null)
        {
            _flow.DefaultAction = saved.DefaultAction;
            _flow.SaveDirectory = saved.SaveDirectory;
        }

        RegisterHotkeys(saved);
    }

    /// <summary>(Re)register the OS-wide hotkeys from the current settings, warning on conflicts.</summary>
    private void RegisterHotkeys(AppSettings settings)
    {
        if (_hotkeys is null)
            return;

        IReadOnlyCollection<HotkeyBinding> bindings = settings.GlobalHotkeysEnabled
            ? BuildBindings(settings)
            : Array.Empty<HotkeyBinding>();

        var results = _hotkeys.Apply(bindings);

        // Surface any chord the OS refused (usually "already in use by another program").
        foreach (var r in results)
        {
            if (r.Ok)
                continue;
            var message = $"Hotkey {r.Hotkey} unavailable: {r.Error}";
            Dispatcher.UIThread.Post(() => ToastService.Show(message));
        }
    }

    private static IReadOnlyCollection<HotkeyBinding> BuildBindings(AppSettings settings)
    {
        var list = new List<HotkeyBinding>(2);
        if (settings.RegionHotkey.IsBound)
            list.Add(new HotkeyBinding(RegionAction, settings.RegionHotkey));
        if (settings.FullScreenHotkey.IsBound)
            list.Add(new HotkeyBinding(FullScreenAction, settings.FullScreenHotkey));
        return list;
    }

    private void OnHotkeyPressed(string action)
    {
        // Pressed may arrive off the UI thread; marshal before any capture / UI work.
        Dispatcher.UIThread.Post(() =>
        {
            switch (action)
            {
                case RegionAction: OnCaptureRegion(this, EventArgs.Empty); break;
                case FullScreenAction: OnCaptureFullScreen(this, EventArgs.Empty); break;
            }
        });
    }

    private void OnQuit(object? sender, EventArgs e)
    {
        _hotkeys?.Dispose();
        _hotkeys = null;

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.Shutdown();
    }
}
