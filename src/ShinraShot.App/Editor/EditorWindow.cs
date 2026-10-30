// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Shinra CC

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using ShinraShot.App.Capture;
using ShinraShot.App.Editor.Model;
using ShinraShot.App.Imaging;
using ShinraShot.App.Notifications;
using ShinraShot.App.Theming;
using ShinraShot.Core.Capture;
using ShinraShot.Core.Imaging;

namespace ShinraShot.App.Editor;

/// <summary>
/// The capture editor: annotate the grab with shapes/arrows/freehand and send it to a
/// destination (copy / save / save-as). Tools and export operate on a flattened render,
/// so what you see is exactly what is copied or saved.
/// </summary>
public sealed class EditorWindow : Window
{
    private readonly CaptureService _svc;
    private EditorCanvas _canvas;
    private ScrollViewer _scroller = null!;
    private readonly List<ToggleButton> _toolButtons = new();
    private Button _undoBtn = null!, _redoBtn = null!;

    private static readonly Color[] SwatchColors =
    {
        Color.FromRgb(0xE0, 0x2B, 0x2B), // accent red
        Color.FromRgb(0xFF, 0xD4, 0x00), // yellow
        Color.FromRgb(0x2B, 0xC2, 0x4A), // green
        Color.FromRgb(0x2B, 0x7F, 0xFF), // blue
        Color.FromRgb(0xFF, 0x8A, 0x1E), // orange
        Color.FromRgb(0xFF, 0xFF, 0xFF), // white
        Color.FromRgb(0x00, 0x00, 0x00), // black
    };

    public EditorWindow(CapturedImage img, WriteableBitmap bmp, byte[] png, CaptureService svc)
    {
        _svc = svc;
        _canvas = new EditorCanvas(img, bmp);
        _canvas.Changed += SyncToolbarState;
        _canvas.CropRequested += OnCropRequested;

        Title = $"ShinraShot Editor — {img.Width}×{img.Height}";
        Width = Math.Min(1280, img.Width + 80);
        Height = Math.Min(860, img.Height + 150);
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        TrySetIcon();

        _scroller = new ScrollViewer
        {
            Content = _canvas,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Background = Palette.CanvasBrush,
        };

        var root = new DockPanel();
        var toolbar = BuildToolbar();
        DockPanel.SetDock(toolbar, Dock.Top);
        root.Children.Add(toolbar);
        root.Children.Add(_scroller);
        Content = root;

        SyncToolbarState();
    }

    private Control BuildToolbar()
    {
        var bar = new WrapPanel { Margin = new Thickness(8, 6) };

        // Tools
        AddTool(bar, "Select", ToolKind.Select, isDefault: true);
        AddTool(bar, "Rect", ToolKind.Rectangle);
        AddTool(bar, "Ellipse", ToolKind.Ellipse);
        AddTool(bar, "Line", ToolKind.Line);
        AddTool(bar, "Arrow", ToolKind.Arrow);
        AddTool(bar, "Pen", ToolKind.Freehand);
        AddTool(bar, "Text", ToolKind.Text);
        AddTool(bar, "Highlight", ToolKind.Highlight);
        AddTool(bar, "Steps", ToolKind.Counter);
        AddTool(bar, "Blur", ToolKind.Blur);
        AddTool(bar, "Pixelate", ToolKind.Pixelate);
        AddTool(bar, "Crop", ToolKind.Crop);

        bar.Children.Add(Separator());

        // Stroke color: a current-color swatch + a quick preset palette.
        bar.Children.Add(Label("Color"));
        var current = new Border
        {
            Width = 22, Height = 22, CornerRadius = new CornerRadius(3),
            Background = new SolidColorBrush(_canvas.Style.StrokeColor),
            BorderBrush = Brushes.White, BorderThickness = new Thickness(1),
            Margin = new Thickness(0, 0, 4, 0), VerticalAlignment = VerticalAlignment.Center,
        };
        bar.Children.Add(current);
        foreach (var c in SwatchColors)
        {
            var cc = c;
            var sw = new Button
            {
                Width = 20, Height = 20, Padding = new Thickness(0), Margin = new Thickness(1, 0),
                Background = new SolidColorBrush(cc), VerticalAlignment = VerticalAlignment.Center,
            };
            sw.Click += (_, _) =>
            {
                _canvas.Style.StrokeColor = cc;
                _canvas.Style.FillColor = Color.FromArgb(0x40, cc.R, cc.G, cc.B);
                current.Background = new SolidColorBrush(cc);
                _canvas.ApplyStyleToSelection();
            };
            bar.Children.Add(sw);
        }

        // Thickness
        var size = new Slider { Minimum = 1, Maximum = 24, Value = _canvas.Style.StrokeThickness, Width = 110, VerticalAlignment = VerticalAlignment.Center };
        size.PropertyChanged += (_, e) =>
        {
            if (e.Property == RangeBase.ValueProperty)
            {
                _canvas.Style.StrokeThickness = size.Value;
                _canvas.ApplyStyleToSelection();
            }
        };
        bar.Children.Add(Label("Size"));
        bar.Children.Add(size);

        // Fill
        var fill = new CheckBox { Content = "Fill", IsChecked = _canvas.Style.Fill, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(6, 0, 0, 0) };
        fill.IsCheckedChanged += (_, _) =>
        {
            _canvas.Style.Fill = fill.IsChecked == true;
            _canvas.ApplyStyleToSelection();
        };
        bar.Children.Add(fill);

        bar.Children.Add(Separator());

        _undoBtn = ActionButton("Undo", () => _canvas.Undo());
        _redoBtn = ActionButton("Redo", () => _canvas.Redo());
        bar.Children.Add(_undoBtn);
        bar.Children.Add(_redoBtn);
        bar.Children.Add(ActionButton("Delete", () => _canvas.DeleteSelected()));

        bar.Children.Add(Separator());

        bar.Children.Add(ActionButton("Copy", DoCopy));
        bar.Children.Add(ActionButton("Save", DoSave));
        bar.Children.Add(ActionButton("Save As…", DoSaveAs));
        bar.Children.Add(PrimaryButton("Done", Close));

        return new Border
        {
            Child = bar,
            Background = Palette.CardBrush,
            BorderBrush = Palette.BorderBrush,
            BorderThickness = new Thickness(0, 0, 0, 1),
        };
    }

    private void AddTool(Panel bar, string label, ToolKind kind, bool isDefault = false)
    {
        var tb = new ToggleButton { Content = label, Padding = new Thickness(10, 6), Margin = new Thickness(2, 0), IsChecked = isDefault };
        tb.Click += (_, _) =>
        {
            foreach (var o in _toolButtons) o.IsChecked = ReferenceEquals(o, tb);
            _canvas.Tool = kind;
        };
        _toolButtons.Add(tb);
        bar.Children.Add(tb);
    }

    private void SelectToolButton(ToolKind kind)
    {
        // Keep the toolbar in sync when the canvas auto-switches to Select after drawing.
        for (int i = 0; i < _toolButtons.Count; i++)
            _toolButtons[i].IsChecked = (ToolKind)i == kind;
    }

    private void SyncToolbarState()
    {
        _undoBtn.IsEnabled = _canvas.CanUndo;
        _redoBtn.IsEnabled = _canvas.CanRedo;
        SelectToolButton(_canvas.Tool);
    }

    // --- destinations (operate on the flattened render) ---

    private void DoCopy()
    {
        var (img, png) = _canvas.Flatten();
        _svc.Copy(img, png);
        ToastService.Show("Copied to clipboard");
    }

    private void DoSave()
    {
        var (_, png) = _canvas.Flatten();
        var p = _svc.SaveToFolder(png);
        ToastService.Show("Saved\n" + System.IO.Path.GetFileName(p), p);
    }

    private async void DoSaveAs()
    {
        var (_, png) = _canvas.Flatten();
        var p = await ImageSink.SaveAsAsync(this, png);
        if (p is not null) ToastService.Show("Saved\n" + System.IO.Path.GetFileName(p), p);
    }

    private void OnCropRequested(ScreenRect r)
    {
        var old = _canvas;
        var cropped = old.BaseImage.Crop(r.X, r.Y, r.Width, r.Height);
        var bmp = AvaloniaImageInterop.ToWriteableBitmap(cropped);

        var next = new EditorCanvas(cropped, bmp) { Style = old.Style, Tool = ToolKind.Select };
        next.Changed += SyncToolbarState;
        next.CropRequested += OnCropRequested;
        next.SeedElements(old.Elements, -r.X, -r.Y);

        _canvas = next;
        _scroller.Content = next;
        Title = $"ShinraShot Editor — {cropped.Width}×{cropped.Height}";
        SyncToolbarState();
    }

    // --- shortcuts ---

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        bool ctrl = e.KeyModifiers.HasFlag(KeyModifiers.Control);
        switch (e.Key)
        {
            case Key.Delete or Key.Back: _canvas.DeleteSelected(); break;
            case Key.Z when ctrl: _canvas.Undo(); break;
            case Key.Y when ctrl: _canvas.Redo(); break;
            case Key.C when ctrl: DoCopy(); break;
            case Key.S when ctrl: DoSave(); break;
        }
    }

    // --- small builders ---

    private static Control Separator() =>
        new Border { Width = 1, Background = Palette.BorderBrush, Margin = new Thickness(6, 3) };

    private static TextBlock Label(string text) =>
        new() { Text = text, Foreground = Palette.SecondaryBrush, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(6, 0, 2, 0) };

    private static Button ActionButton(string text, Action onClick)
    {
        var b = new Button { Content = text, Padding = new Thickness(10, 6), Margin = new Thickness(2, 0) };
        b.Click += (_, _) => onClick();
        return b;
    }

    private static Button PrimaryButton(string text, Action onClick)
    {
        var b = ActionButton(text, onClick);
        b.Classes.Add("accent");
        return b;
    }

    private void TrySetIcon()
    {
        try { Icon = new WindowIcon(new Bitmap(AssetLoader.Open(new Uri("avares://ShinraShot/Assets/shinrashot.png")))); }
        catch { /* ignore */ }
    }
}
