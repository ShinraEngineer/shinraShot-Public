// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Shinra CC

using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using ShinraShot.Core.Capture;
using ShinraShot.Core.Imaging;

namespace ShinraShot.App.Overlay;

/// <summary>
/// Full-virtual-desktop region selector. Displays a frozen grab of the desktop and lets the
/// user drag a rectangle over it (so the dim veil / crosshair / magnifier are never part of
/// the shot). Returns the selection in image-local pixels, or null if cancelled.
/// </summary>
public sealed class RegionOverlayWindow : Window
{
    private readonly OverlayView _view;
    private readonly TaskCompletionSource<ScreenRect?> _tcs = new();

    public RegionOverlayWindow(CapturedImage image, WriteableBitmap bitmap, ScreenRect virtualBounds, double scale)
    {
        WindowDecorations = WindowDecorations.None;
        Topmost = true;
        ShowInTaskbar = false;
        CanResize = false;
        Background = Brushes.Black;
        WindowStartupLocation = WindowStartupLocation.Manual;
        Cursor = new Cursor(StandardCursorType.Cross);
        Position = new PixelPoint(virtualBounds.X, virtualBounds.Y);
        Width = virtualBounds.Width / scale;
        Height = virtualBounds.Height / scale;

        _view = new OverlayView(image, bitmap, scale);
        RenderOptions.SetBitmapInterpolationMode(_view, BitmapInterpolationMode.None);
        _view.Confirmed += r => { _tcs.TrySetResult(r); Close(); };
        _view.Cancelled += () => { _tcs.TrySetResult(null); Close(); };
        Content = _view;

        Opened += (_, _) => _view.Focus();
        Closed += (_, _) => _tcs.TrySetResult(null);
    }

    public Task<ScreenRect?> PickAsync()
    {
        Show();
        Activate();
        return _tcs.Task;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key == Key.Escape) _view.Cancel();
        else if (e.Key is Key.Enter or Key.Return) _view.ConfirmIfValid();
    }

    /// <summary>Test seam: preset the selection/cursor so the overlay can be rendered headless.</summary>
    internal OverlayView View => _view;
}

/// <summary>The drawn surface of the region overlay: screenshot, veil, crosshair, magnifier.</summary>
public sealed class OverlayView : Control
{
    private static readonly IBrush Veil = new SolidColorBrush(Color.FromArgb(0x7A, 0x00, 0x00, 0x00));
    private static readonly Color Accent = Color.FromRgb(0xE0, 0x2B, 0x2B);
    private static readonly IBrush AccentBrush = new SolidColorBrush(Accent);
    private static readonly IPen SelectionPen = new Pen(AccentBrush, 1.5);
    private static readonly IPen CrosshairPen = new Pen(new SolidColorBrush(Color.FromArgb(0xAA, 0xE0, 0x2B, 0x2B)), 1);
    private static readonly IPen MagBorderPen = new Pen(AccentBrush, 2);
    private static readonly IPen MagCenterPen = new Pen(new SolidColorBrush(Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF)), 1);
    // Reticle arms inside the magnifier. A thin dark underlay plus a bright accent line keeps the
    // crosshair legible over any pixel content, so an edge in the zoom can be lined up precisely.
    private static readonly IPen MagReticleUnderPen = new Pen(new SolidColorBrush(Color.FromArgb(0x80, 0x00, 0x00, 0x00)), 2);
    private static readonly IPen MagReticlePen = new Pen(new SolidColorBrush(Color.FromArgb(0xE6, 0xE0, 0x2B, 0x2B)), 1);
    private static readonly IBrush LabelBg = new SolidColorBrush(Color.FromArgb(0xD8, 0x10, 0x10, 0x10));
    private static readonly Typeface Face = new("Segoe UI");

    private const int ZoomHalf = 9;       // samples a (2*ZoomHalf+1) px square
    private const double MagBox = 132;     // magnifier box edge, DIP

    private readonly CapturedImage _img;
    private readonly WriteableBitmap _bmp;
    private readonly double _scale;
    private readonly int _imgW, _imgH;

    private bool _hasCursor;
    private Point _cursor;
    private bool _selActive;
    private Point _start, _cur;

    public event Action<ScreenRect>? Confirmed;
    public event Action? Cancelled;

    public OverlayView(CapturedImage image, WriteableBitmap bitmap, double scale)
    {
        _img = image;
        _bmp = bitmap;
        _scale = scale;
        _imgW = image.Width;
        _imgH = image.Height;
        Focusable = true;
    }

    public void Cancel() => Cancelled?.Invoke();

    public void ConfirmIfValid()
    {
        if (!_selActive) return;
        var r = SelRectImage();
        if (r.Width >= 2 && r.Height >= 2) Confirmed?.Invoke(r);
    }

    // --- pointer ---

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        _cursor = _start = _cur = e.GetPosition(this);
        _hasCursor = true;
        _selActive = true;
        e.Pointer.Capture(this);
        InvalidateVisual();
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        _cursor = e.GetPosition(this);
        _hasCursor = true;
        if (_selActive) _cur = _cursor;
        InvalidateVisual();
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (e.InitialPressMouseButton != MouseButton.Left) return;
        e.Pointer.Capture(null);
        if (_selActive)
        {
            var r = SelRectImage();
            if (r.Width >= 2 && r.Height >= 2) { Confirmed?.Invoke(r); return; }
            _selActive = false;   // too small — treat as a miss, keep selecting
            InvalidateVisual();
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key == Key.Escape) Cancel();
        else if (e.Key is Key.Enter or Key.Return) ConfirmIfValid();
    }

    // --- geometry ---

    private Rect SelRectDip()
    {
        double x = Math.Min(_start.X, _cur.X), y = Math.Min(_start.Y, _cur.Y);
        double w = Math.Abs(_cur.X - _start.X), h = Math.Abs(_cur.Y - _start.Y);
        return new Rect(x, y, w, h);
    }

    private ScreenRect SelRectImage()
    {
        var r = SelRectDip();
        int x = (int)Math.Round(r.X * _scale);
        int y = (int)Math.Round(r.Y * _scale);
        int w = (int)Math.Round(r.Width * _scale);
        int h = (int)Math.Round(r.Height * _scale);
        x = Math.Clamp(x, 0, _imgW - 1);
        y = Math.Clamp(y, 0, _imgH - 1);
        w = Math.Clamp(w, 1, _imgW - x);
        h = Math.Clamp(h, 1, _imgH - y);
        return new ScreenRect(x, y, w, h);
    }

    // --- render ---

    public override void Render(DrawingContext ctx)
    {
        double cw = Bounds.Width, ch = Bounds.Height;
        var full = new Rect(0, 0, cw, ch);

        // Frozen screenshot, 1:1 into the control.
        ctx.DrawImage(_bmp, full);

        if (_selActive)
        {
            var sel = SelRectDip();
            // Dim everything outside the selection (four bands).
            ctx.FillRectangle(Veil, new Rect(0, 0, cw, sel.Y));
            ctx.FillRectangle(Veil, new Rect(0, sel.Bottom, cw, Math.Max(0, ch - sel.Bottom)));
            ctx.FillRectangle(Veil, new Rect(0, sel.Y, sel.X, sel.Height));
            ctx.FillRectangle(Veil, new Rect(sel.Right, sel.Y, Math.Max(0, cw - sel.Right), sel.Height));
            ctx.DrawRectangle(null, SelectionPen, sel);
            DrawDimensions(ctx, sel);
        }
        else
        {
            ctx.FillRectangle(Veil, full);
            DrawHint(ctx, cw, ch);
        }

        if (_hasCursor)
        {
            // Crosshair across the whole overlay.
            ctx.DrawLine(CrosshairPen, new Point(0, _cursor.Y), new Point(cw, _cursor.Y));
            ctx.DrawLine(CrosshairPen, new Point(_cursor.X, 0), new Point(_cursor.X, ch));
            DrawMagnifier(ctx, cw, ch);
        }
    }

    private void DrawDimensions(DrawingContext ctx, Rect sel)
    {
        var img = SelRectImage();
        var ft = Text($"{img.Width} × {img.Height}", 12, Brushes.White);
        double pad = 5;
        double lx = sel.X;
        double ly = sel.Y - ft.Height - 2 * pad - 2;
        if (ly < 2) ly = sel.Y + 4;                       // flip below if no room above
        var bg = new Rect(lx, ly, ft.Width + 2 * pad, ft.Height + 2 * pad);
        ctx.FillRectangle(LabelBg, bg, 3);
        ctx.DrawText(ft, new Point(lx + pad, ly + pad));
    }

    private void DrawHint(DrawingContext ctx, double cw, double ch)
    {
        var ft = Text("Drag to select a region   ·   Esc to cancel   ·   Enter to confirm", 15, Brushes.White);
        double pad = 12;
        double x = (cw - ft.Width) / 2 - pad;
        double y = ch * 0.08;
        var bg = new Rect(x, y, ft.Width + 2 * pad, ft.Height + 2 * pad);
        ctx.FillRectangle(LabelBg, bg, 6);
        ctx.DrawRectangle(null, new Pen(AccentBrush, 1), bg, 6);
        ctx.DrawText(ft, new Point(x + pad, y + pad));
    }

    private void DrawMagnifier(DrawingContext ctx, double cw, double ch)
    {
        int cxImg = Math.Clamp((int)Math.Round(_cursor.X * _scale), 0, _imgW - 1);
        int cyImg = Math.Clamp((int)Math.Round(_cursor.Y * _scale), 0, _imgH - 1);

        int srcS = ZoomHalf * 2 + 1;
        double sx = Math.Clamp(cxImg - ZoomHalf, 0, Math.Max(0, _imgW - srcS));
        double sy = Math.Clamp(cyImg - ZoomHalf, 0, Math.Max(0, _imgH - srcS));

        // Position the box offset from the cursor, flipping away from screen edges.
        double off = 22;
        double mx = _cursor.X + off, my = _cursor.Y + off;
        double labelH = 38;
        if (mx + MagBox > cw) mx = _cursor.X - off - MagBox;
        if (my + MagBox + labelH > ch) my = _cursor.Y - off - MagBox - labelH;
        if (mx < 0) mx = 2;
        if (my < 0) my = 2;

        var dst = new Rect(mx, my, MagBox, MagBox);
        ctx.DrawImage(_bmp, new Rect(sx, sy, srcS, srcS), dst);
        ctx.DrawRectangle(null, MagBorderPen, dst);

        // Reticle: crosshair arms spanning the magnifier, meeting at the hovered pixel, with the
        // pixel itself left boxed in the gap. This is the Greenshot-style aid for lining an edge
        // in the zoom up against the crosshair. Tracks the true hovered pixel even when the source
        // is clamped at a screen edge.
        double pxScale = MagBox / srcS;
        double markerX = mx + (cxImg - sx) * pxScale;
        double markerY = my + (cyImg - sy) * pxScale;
        double midX = markerX + pxScale / 2;
        double midY = markerY + pxScale / 2;

        using (ctx.PushClip(dst))
        {
            foreach (var pen in new[] { MagReticleUnderPen, MagReticlePen })
            {
                // Horizontal arm, broken across the target pixel cell.
                ctx.DrawLine(pen, new Point(dst.Left, midY), new Point(markerX, midY));
                ctx.DrawLine(pen, new Point(markerX + pxScale, midY), new Point(dst.Right, midY));
                // Vertical arm, broken across the target pixel cell.
                ctx.DrawLine(pen, new Point(midX, dst.Top), new Point(midX, markerY));
                ctx.DrawLine(pen, new Point(midX, markerY + pxScale), new Point(midX, dst.Bottom));
            }
        }

        // Boxed center pixel marker, drawn last so it sits on top of the reticle arms.
        ctx.DrawRectangle(null, MagCenterPen, new Rect(markerX, markerY, pxScale, pxScale));

        // Readout: absolute virtual coords + hovered color.
        string hex = _img.ColorHexAt(cxImg, cyImg);
        int ax = _img.OriginX + cxImg, ay = _img.OriginY + cyImg;
        var info = Text($"{ax}, {ay}\n{hex}", 11, Brushes.White);
        var infoBg = new Rect(mx, my + MagBox + 2, MagBox, labelH - 2);
        ctx.FillRectangle(LabelBg, infoBg, 3);
        var swatch = new Rect(mx + MagBox - 20, my + MagBox + 10, 12, 12);
        ctx.FillRectangle(new SolidColorBrush(Parse(hex)), swatch);
        ctx.DrawRectangle(null, new Pen(Brushes.White, 0.5), swatch);
        ctx.DrawText(info, new Point(mx + 6, my + MagBox + 5));
    }

    private static FormattedText Text(string s, double size, IBrush brush) =>
        new(s, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Face, size, brush);

    private static Color Parse(string hex) =>
        Color.FromRgb(
            Convert.ToByte(hex.Substring(1, 2), 16),
            Convert.ToByte(hex.Substring(3, 2), 16),
            Convert.ToByte(hex.Substring(5, 2), 16));

    /// <summary>Test seam: preset a selection + cursor so Render can be exercised headless.</summary>
    internal void PresetForRender(Point cursor, Point start, Point cur)
    {
        _hasCursor = true;
        _cursor = cursor;
        _selActive = true;
        _start = start;
        _cur = cur;
    }
}
