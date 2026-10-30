// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Shinra CC

using System.IO;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using ShinraShot.App.Capture;
using ShinraShot.App.Editor;
using ShinraShot.App.Editor.Model;
using ShinraShot.App.Imaging;
using ShinraShot.App.Overlay;
using ShinraShot.App.Settings;
using ShinraShot.Core.Capture;
using ShinraShot.Core.Input;
using ShinraShot.Core.Platform;
using ShinraShot.Core.Settings;
using ShinraShot.Core.Update;

namespace ShinraShot.App;

internal static class Program
{
    // Avalonia configuration; the SynchronizationContext-reliant bits must run after this.
    [STAThread]
    public static void Main(string[] args)
    {
        // Give a native Wayland capture backend the chance to register itself before any
        // backend is created. No-op today (Wayland runs through XWayland); see LinuxWaylandCapture.
        if (OperatingSystem.IsLinux())
            Platform.Linux.LinuxWaylandCapture.TryRegister();

        // Dev/diagnostic: print the detected displays (bounds + scale) and exit.
        //   ShinraShot.exe --screens
        if (args.Length > 0 && args[0] == "--screens")
        {
            var backend = ShinraShot.Core.Platform.PlatformServices.CreateCaptureBackend();
            var vb = backend.VirtualBounds;
            Console.Error.WriteLine($"[screens] virtual = {vb.X},{vb.Y} {vb.Width}x{vb.Height}");
            foreach (var s in backend.GetScreens())
                Console.Error.WriteLine(
                    $"  {s.Name}: {s.Bounds.X},{s.Bounds.Y} {s.Bounds.Width}x{s.Bounds.Height}" +
                    $"  scale={s.Scale:0.##}{(s.IsPrimary ? "  [primary]" : "")}");
            return;
        }

        // Dev/diagnostic: grab the whole desktop, save a PNG, and exit. No UI loop.
        //   ShinraShot.exe --capture-selftest [outputDir]
        if (args.Length > 0 && args[0] == "--capture-selftest")
        {
            RunCaptureSelfTest(args);
            return;
        }

        // Dev/diagnostic: render the region overlay (with a preset selection) to a PNG.
        //   ShinraShot.exe --overlay-selftest [outputDir]
        if (args.Length > 0 && args[0] == "--overlay-selftest")
        {
            RunOverlaySelfTest(args);
            return;
        }

        // Dev/diagnostic: flatten a capture with sample annotations to a PNG.
        //   ShinraShot.exe --editor-selftest [outputDir]
        if (args.Length > 0 && args[0] == "--editor-selftest")
        {
            RunEditorSelfTest(args);
            return;
        }

        // Dev/diagnostic: open the editor window on a live capture (to view the theme).
        //   ShinraShot.exe --editor-demo
        if (args.Length > 0 && args[0] == "--editor-demo")
        {
            RunEditorDemo(args);
            return;
        }

        // Dev/diagnostic: print the resolved settings path + values and round-trip the schema.
        //   ShinraShot.exe --settings-selftest
        if (args.Length > 0 && args[0] == "--settings-selftest")
        {
            RunSettingsSelfTest();
            return;
        }

        // Dev/diagnostic: open the settings window (to view the theme / try the hotkey capture).
        //   ShinraShot.exe --settings-demo
        if (args.Length > 0 && args[0] == "--settings-demo")
        {
            RunSettingsDemo(args);
            return;
        }

        // Dev/diagnostic: register the default capture hotkeys and report what the OS accepted.
        //   ShinraShot.exe --hotkeys-selftest
        if (args.Length > 0 && args[0] == "--hotkeys-selftest")
        {
            RunHotkeysSelfTest();
            return;
        }

        // Dev/diagnostic: check an update feed and print the result. No UI loop.
        //   ShinraShot.exe --update-check [feedUrl] [--pre]
        if (args.Length > 0 && args[0] == "--update-check")
        {
            RunUpdateCheck(args).GetAwaiter().GetResult();
            return;
        }

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    private static async Task RunUpdateCheck(string[] args)
    {
        string? feed = args.Length > 1 && !args[1].StartsWith("--") ? args[1] : null;
        bool pre = Array.IndexOf(args, "--pre") >= 0;

        var result = await new UpdateService(feed).CheckAsync(pre);
        Console.Error.WriteLine($"[update] feed      = {feed ?? UpdateService.DefaultFeedUrl}");
        Console.Error.WriteLine($"[update] current   = {result.CurrentVersion}");
        Console.Error.WriteLine($"[update] success   = {result.Success}");
        Console.Error.WriteLine($"[update] latest    = {result.LatestVersion ?? "(none)"}");
        Console.Error.WriteLine($"[update] available = {result.UpdateAvailable}");
        Console.Error.WriteLine($"[update] download  = {result.DownloadUrl ?? "(none)"}");
        if (result.Message is not null)
            Console.Error.WriteLine($"[update] message   = {result.Message}");
    }

    private static void RunHotkeysSelfTest()
    {
        using var hotkeys = PlatformServices.CreateGlobalHotkeys();
        var defaults = new AppSettings();

        var bindings = new List<HotkeyBinding>();
        if (defaults.RegionHotkey.IsBound) bindings.Add(new HotkeyBinding("region", defaults.RegionHotkey));
        if (defaults.FullScreenHotkey.IsBound) bindings.Add(new HotkeyBinding("fullscreen", defaults.FullScreenHotkey));

        foreach (var r in hotkeys.Apply(bindings))
            Console.Error.WriteLine($"[hotkeys] {r.Id,-11} {r.Hotkey,-20} {(r.Ok ? "OK" : "FAILED: " + r.Error)}");
    }

    private static void RunSettingsDemo(string[] args)
    {
        var lifetime = new Avalonia.Controls.ApplicationLifetimes.ClassicDesktopStyleApplicationLifetime { Args = args };
        BuildAvaloniaApp().SetupWithLifetime(lifetime);

        var store = new SettingsStore();
        var win = new SettingsWindow(store.Load(), s =>
        {
            store.Save(s);
            Console.Error.WriteLine(
                $"[settings-demo] saved: default={s.DefaultAction}, region={s.RegionHotkey}, " +
                $"full={s.FullScreenHotkey}, dir={s.SaveDirectory ?? "(default)"}");
        });
        lifetime.MainWindow = win;
        lifetime.Start(args);
    }

    private static void RunSettingsSelfTest()
    {
        var store = new SettingsStore();
        var loaded = store.Load();
        Console.Error.WriteLine($"[settings] file      = {store.FilePath}");
        Console.Error.WriteLine($"[settings] exists    = {File.Exists(store.FilePath)}");
        Console.Error.WriteLine($"[settings] default   = {loaded.DefaultAction}");
        Console.Error.WriteLine($"[settings] region    = {loaded.RegionHotkey}");
        Console.Error.WriteLine($"[settings] fullscreen= {loaded.FullScreenHotkey}");
        Console.Error.WriteLine($"[settings] hotkeys   = {(loaded.GlobalHotkeysEnabled ? "enabled" : "disabled")}");
        Console.Error.WriteLine($"[settings] saveDir   = {loaded.SaveDirectory ?? "(default) " + CaptureOptions.DefaultDirectory}");

        // Round-trip a sample through a temp file to prove serialize/deserialize fidelity.
        string tmp = Path.Combine(Path.GetTempPath(), "shinrashot-settings-selftest.json");
        var sample = new AppSettings
        {
            DefaultAction = DefaultCaptureAction.OpenInEditor,
            RegionHotkey = new Hotkey(HotkeyModifiers.Control | HotkeyModifiers.Shift, "S"),
            FullScreenHotkey = Hotkey.None,
            SaveDirectory = @"X:\shots",
        };
        var tmpStore = new SettingsStore(tmp);
        tmpStore.Save(sample);
        var back = tmpStore.Load();
        bool ok = back.DefaultAction == sample.DefaultAction
               && back.RegionHotkey == sample.RegionHotkey
               && back.FullScreenHotkey == sample.FullScreenHotkey
               && back.SaveDirectory == sample.SaveDirectory;
        Console.Error.WriteLine($"[settings] round-trip {(ok ? "OK" : "FAILED")} via {tmp}");
        try { File.Delete(tmp); } catch { /* best effort */ }
    }

    private static void RunCaptureSelfTest(string[] args)
    {
        // SetupWithoutStarting initializes the Avalonia platform (incl. Skia) on this
        // thread without a message loop, so bitmap conversion + PNG encode work headless.
        BuildAvaloniaApp().SetupWithoutStarting();

        bool clip = Array.IndexOf(args, "--clip") >= 0;
        var options = new CaptureOptions { CopyToClipboard = clip };
        if (args.Length > 1 && args[1] != "--clip") options.SaveDirectory = args[1];

        var outcome = new CaptureService().CaptureFullScreen(options);
        Console.Error.WriteLine(
            $"[selftest] {outcome.Image.Width}x{outcome.Image.Height}, " +
            $"png={outcome.PngByteCount} bytes, saved={outcome.SavedPath}");
    }

    private static void RunOverlaySelfTest(string[] args)
    {
        BuildAvaloniaApp().SetupWithoutStarting();

        var svc = new CaptureService();
        var frozen = svc.Grab();
        var bmp = AvaloniaImageInterop.ToWriteableBitmap(frozen);
        var vb = svc.Backend.VirtualBounds;

        double scale = 1.0;
        foreach (var s in svc.Backend.GetScreens())
            if (s.IsPrimary && s.Scale > 0) scale = s.Scale;

        double cw = vb.Width / scale, ch = vb.Height / scale;
        var view = new OverlayView(frozen, bmp, scale);
        RenderOptions.SetBitmapInterpolationMode(view, BitmapInterpolationMode.None);
        view.PresetForRender(new Point(cw * 0.5, ch * 0.46),
                             new Point(cw * 0.30, ch * 0.32),
                             new Point(cw * 0.66, ch * 0.60));
        view.Measure(new Size(cw, ch));
        view.Arrange(new Rect(0, 0, cw, ch));

        var rtb = new RenderTargetBitmap(new PixelSize((int)Math.Round(cw), (int)Math.Round(ch)), new Vector(96, 96));
        rtb.Render(view);

        string dir = args.Length > 1 ? args[1] : Directory.GetCurrentDirectory();
        string outPath = Path.Combine(dir, "overlay-preview.png");
#pragma warning disable CS0618
        using (var fs = File.Create(outPath)) rtb.Save(fs);
#pragma warning restore CS0618
        Console.Error.WriteLine($"[overlay-selftest] saved {outPath}");
    }

    private static void RunEditorSelfTest(string[] args)
    {
        BuildAvaloniaApp().SetupWithoutStarting();

        var svc = new CaptureService();
        var img = svc.Grab();
        var bmp = AvaloniaImageInterop.ToWriteableBitmap(img);
        var canvas = new EditorCanvas(img, bmp);

        canvas.AddElementForTest(new RectangleElement
        {
            Start = new Point(90, 90), End = new Point(520, 360),
            Style = new ToolStyle { StrokeColor = Color.FromRgb(0xE0, 0x2B, 0x2B), StrokeThickness = 5 },
        });
        canvas.AddElementForTest(new EllipseElement
        {
            Start = new Point(560, 120), End = new Point(840, 340),
            Style = new ToolStyle { StrokeColor = Color.FromRgb(0x2B, 0x7F, 0xFF), StrokeThickness = 5, Fill = true, FillColor = Color.FromArgb(0x40, 0x2B, 0x7F, 0xFF) },
        });
        canvas.AddElementForTest(new ArrowElement
        {
            Start = new Point(200, 520), End = new Point(700, 650),
            Style = new ToolStyle { StrokeColor = Color.FromRgb(0xFF, 0xD4, 0x00), StrokeThickness = 6 },
        });
        var fh = new FreehandElement { Style = new ToolStyle { StrokeColor = Color.FromRgb(0x2B, 0xC2, 0x4A), StrokeThickness = 4 } };
        for (double a = 0; a < 6.3; a += 0.08)
            fh.Points.Add(new Point(1000 + Math.Cos(a) * 90, 470 + Math.Sin(a) * 90));
        canvas.AddElementForTest(fh);

        canvas.AddElementForTest(new HighlightElement
        {
            Start = new Point(120, 400), End = new Point(520, 445),
            Style = new ToolStyle { StrokeColor = Color.FromRgb(0xFF, 0xD4, 0x00) },
        });
        canvas.AddElementForTest(new TextElement
        {
            Position = new Point(120, 700), Text = "ShinraShot editor",
            Style = new ToolStyle { StrokeColor = Color.FromRgb(0xFF, 0xFF, 0xFF), FontSize = 34 },
        });
        canvas.AddElementForTest(new CounterElement { Center = new Point(560, 470), Number = 1, Style = new ToolStyle { StrokeColor = Color.FromRgb(0xE0, 0x2B, 0x2B), FontSize = 22 } });
        canvas.AddElementForTest(new CounterElement { Center = new Point(760, 540), Number = 2, Style = new ToolStyle { StrokeColor = Color.FromRgb(0xE0, 0x2B, 0x2B), FontSize = 22 } });
        canvas.AddElementForTest(new ObfuscateElement { Start = new Point(1150, 130), End = new Point(1430, 310), Mode = ObfuscateMode.Pixelate, Base = img, Style = new ToolStyle { StrokeThickness = 6 } });
        canvas.AddElementForTest(new ObfuscateElement { Start = new Point(1150, 340), End = new Point(1430, 520), Mode = ObfuscateMode.Blur, Base = img, Style = new ToolStyle { StrokeThickness = 7 } });

        var (_, png) = canvas.Flatten();
        string dir = args.Length > 1 ? args[1] : Directory.GetCurrentDirectory();
        string outPath = Path.Combine(dir, "editor-preview.png");
        File.WriteAllBytes(outPath, png);
        Console.Error.WriteLine($"[editor-selftest] {canvas.ElementCount} elements, saved {outPath} ({png.Length} bytes)");
    }

    private static void RunEditorDemo(string[] args)
    {
        var lifetime = new Avalonia.Controls.ApplicationLifetimes.ClassicDesktopStyleApplicationLifetime { Args = args };
        BuildAvaloniaApp().SetupWithLifetime(lifetime);

        var svc = new CaptureService();
        var img = svc.Grab();
        var bmp = AvaloniaImageInterop.ToWriteableBitmap(img);
        byte[] png = AvaloniaImageInterop.EncodePng(bmp);

        var win = new EditorWindow(img, bmp, png, svc);
        lifetime.MainWindow = win;
        lifetime.Start(args);
    }

    // Used by the Avalonia designer and by Main.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
