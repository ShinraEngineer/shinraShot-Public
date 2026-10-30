// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Shinra CC

using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using ShinraShot.App.Imaging;
using ShinraShot.App.Editor.Model;
using ShinraShot.Core.Capture;
using ShinraShot.Core.Imaging;

namespace ShinraShot.App.Editor;

/// <summary>
/// The drawing surface. <see cref="Panel.Render"/> is sealed, so this is a <see cref="Grid"/>
/// that composites a custom render surface (the capture + annotations, drawn 1:1 where control
/// DIP == image pixel) with a transparent overlay <see cref="Canvas"/> that hosts the inline text
/// editor. Pointer events bubble from the surface to this grid's handlers. Export flattens base +
/// elements to a PNG and a CapturedImage (for the clipboard).
/// </summary>
public sealed class EditorCanvas : Grid
{
    private static readonly IPen SelectionPen =
        new Pen(new SolidColorBrush(Color.FromArgb(0xFF, 0x4A, 0xA3, 0xFF)), 1,
            dashStyle: new DashStyle(new double[] { 4, 3 }, 0));
    private static readonly IBrush HandleFill = Brushes.White;
    private static readonly IPen HandlePen = new Pen(new SolidColorBrush(Color.FromRgb(0x4A, 0xA3, 0xFF)), 1);

    private readonly CapturedImage _img;
    private readonly WriteableBitmap _baseBmp;
    private readonly EditorDocument _doc;
    private readonly RenderSurface _surface;
    private readonly Canvas _overlay;

    private enum HandleId { None, TopLeft, Top, TopRight, Right, BottomRight, Bottom, BottomLeft, Left, P1, P2 }

    private AnnotationElement? _selected;
    private AnnotationElement? _pending;
    private bool _creating, _moving, _movePushed, _resizing;
    private HandleId _resizeHandle;
    private Point _lastPt;
    private int _nextCounter = 1;

    private TextBox? _editBox;
    private TextElement? _editing;
    private bool _editingExisting;

    private bool _cropping;
    private Point _cropStart, _cropEnd;

    public EditorCanvas(CapturedImage img, WriteableBitmap baseBmp)
    {
        _img = img;
        _baseBmp = baseBmp;
        _doc = new EditorDocument(img.Width, img.Height);
        Width = img.Width;
        Height = img.Height;
        Focusable = true;

        _surface = new RenderSurface(this);
        _overlay = new Canvas { Background = null };
        Children.Add(_surface);
        Children.Add(_overlay);
    }

    public ToolKind Tool { get; set; } = ToolKind.Select;
    public ToolStyle Style { get; set; } = new();

    internal void AddElementForTest(AnnotationElement el)
    {
        _doc.Elements.Add(el);
        Invalidate();
    }

    internal int ElementCount => _doc.Elements.Count;

    public bool CanUndo => _doc.CanUndo;
    public bool CanRedo => _doc.CanRedo;
    public bool HasSelection => _selected is not null;

    public event Action? Changed;

    /// <summary>Raised when the Crop tool finishes a selection (region in image pixels).</summary>
    public event Action<ScreenRect>? CropRequested;

    internal IReadOnlyList<AnnotationElement> Elements => _doc.Elements;
    internal CapturedImage BaseImage => _img;

    /// <summary>Seed cloned elements (shifted by dx,dy) into a freshly cropped canvas.</summary>
    internal void SeedElements(IEnumerable<AnnotationElement> elements, double dx, double dy)
    {
        foreach (var e in elements)
        {
            var c = e.Clone();
            c.Move(dx, dy);
            if (c is ObfuscateElement o) o.Base = _img;   // re-point at the new (cropped) base
            _doc.Elements.Add(c);
        }
        Invalidate();
    }

    private void Invalidate() => _surface.InvalidateVisual();

    // --- editing commands ---

    public void Undo()
    {
        CommitTextEdit();
        _doc.Undo();
        _selected = null;
        Refresh();
    }

    public void Redo()
    {
        CommitTextEdit();
        _doc.Redo();
        _selected = null;
        Refresh();
    }

    public void DeleteSelected()
    {
        CommitTextEdit();
        if (_selected is null) return;
        _doc.PushUndo();
        _doc.Elements.Remove(_selected);
        _selected = null;
        Refresh();
    }

    public void ApplyStyleToSelection()
    {
        if (_selected is null) return;
        _selected.Style = Style.Clone();
        Invalidate();
    }

    // --- pointer ---

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;

        if (_editBox is not null) { CommitTextEdit(); return; }

        var pt = e.GetPosition(this);
        Focus();

        switch (Tool)
        {
            case ToolKind.Select:
                // A resize handle on the current selection takes priority over picking another element.
                if (_selected is not null)
                {
                    var h = HandleAt(pt, _selected);
                    if (h != HandleId.None)
                    {
                        _resizing = true;
                        _resizeHandle = h;
                        _movePushed = false;
                        _lastPt = pt;
                        e.Pointer.Capture(this);
                        break;
                    }
                }

                var hit = HitTest(pt);
                if (hit is TextElement te && e.ClickCount >= 2)
                {
                    _selected = hit;
                    BeginTextEdit(te.Position, te);
                    return;
                }
                _selected = hit;
                if (hit is not null)
                {
                    _moving = true;
                    _movePushed = false;
                    _lastPt = pt;
                    e.Pointer.Capture(this);
                }
                Refresh();
                break;

            case ToolKind.Text:
                BeginTextEdit(pt, null);
                break;

            case ToolKind.Counter:
                _doc.PushUndo();
                var counter = new CounterElement { Center = pt, Number = _nextCounter++, Style = Style.Clone() };
                _doc.Elements.Add(counter);
                _selected = counter;
                Refresh();
                break;

            case ToolKind.Crop:
                _cropping = true;
                _cropStart = _cropEnd = pt;
                e.Pointer.Capture(this);
                Invalidate();
                break;

            default:
                _creating = true;
                _pending = NewElement(pt);
                e.Pointer.Capture(this);
                break;
        }
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var pt = e.GetPosition(this);

        if (_cropping)
        {
            _cropEnd = pt;
            Invalidate();
        }
        else if (_creating && _pending is not null)
        {
            if (_pending is FreehandElement fh) fh.Points.Add(pt);
            else if (_pending is TwoPointElement tp) tp.End = pt;
            Invalidate();
        }
        else if (_resizing && _selected is not null)
        {
            if (!_movePushed) { _doc.PushUndo(); _movePushed = true; }
            ApplyResize(_selected, _resizeHandle, pt);
            Invalidate();
        }
        else if (_moving && _selected is not null)
        {
            if (!_movePushed) { _doc.PushUndo(); _movePushed = true; }
            var d = pt - _lastPt;
            _selected.Move(d.X, d.Y);
            _lastPt = pt;
            Invalidate();
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (e.InitialPressMouseButton != MouseButton.Left) return;
        e.Pointer.Capture(null);

        if (_cropping)
        {
            _cropping = false;
            var r = NormalizedImageRect(_cropStart, _cropEnd);
            Invalidate();
            if (r.Width >= 4 && r.Height >= 4) CropRequested?.Invoke(r);
            return;
        }

        if (_creating && _pending is not null)
        {
            if (IsValid(_pending))
            {
                _doc.PushUndo();
                _doc.Elements.Add(_pending);
                _selected = _pending;
                Tool = ToolKind.Select;
            }
            _pending = null;
            _creating = false;
            Refresh();
        }
        else if (_moving || _resizing)
        {
            _moving = false;
            _resizing = false;
            Refresh();
        }
    }

    // --- inline text editing ---

    private void BeginTextEdit(Point pt, TextElement? existing)
    {
        CommitTextEdit();

        _editing = existing ?? new TextElement { Position = pt, Style = Style.Clone() };
        _editingExisting = existing is not null;

        _editBox = new TextBox
        {
            Text = _editing.Text,
            FontSize = _editing.Style.FontSize,
            Foreground = new SolidColorBrush(_editing.Style.StrokeColor),
            Background = new SolidColorBrush(Color.FromArgb(0x40, 0x00, 0x00, 0x00)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(0xFF, 0x4A, 0xA3, 0xFF)),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(2),
            MinWidth = 60,
            AcceptsReturn = true,
        };
        Canvas.SetLeft(_editBox, _editing.Position.X);
        Canvas.SetTop(_editBox, _editing.Position.Y);
        _overlay.Children.Add(_editBox);

        _editBox.KeyDown += (_, ke) =>
        {
            if (ke.Key == Key.Enter && !ke.KeyModifiers.HasFlag(KeyModifiers.Shift)) { ke.Handled = true; CommitTextEdit(); }
            else if (ke.Key == Key.Escape) { ke.Handled = true; CancelTextEdit(); }
        };
        _editBox.LostFocus += (_, _) => CommitTextEdit();

        var box = _editBox;
        Dispatcher.UIThread.Post(() =>
        {
            box.Focus();
            box.CaretIndex = box.Text?.Length ?? 0;
        });
    }

    private void CommitTextEdit()
    {
        if (_editBox is null || _editing is null) return;

        string text = (_editBox.Text ?? string.Empty).TrimEnd();
        var box = _editBox;
        var el = _editing;
        _editBox = null;
        _editing = null;
        _overlay.Children.Remove(box);

        if (text.Length == 0)
        {
            if (_editingExisting) { _doc.PushUndo(); _doc.Elements.Remove(el); }
        }
        else
        {
            _doc.PushUndo();
            el.Text = text;
            if (!_editingExisting) _doc.Elements.Add(el);
            _selected = el;
        }

        Tool = ToolKind.Select;
        Refresh();
    }

    private void CancelTextEdit()
    {
        if (_editBox is null) return;
        _overlay.Children.Remove(_editBox);
        _editBox = null;
        _editing = null;
        Invalidate();
    }

    // --- model helpers ---

    private AnnotationElement NewElement(Point pt)
    {
        AnnotationElement el = Tool switch
        {
            ToolKind.Rectangle => new RectangleElement { Start = pt, End = pt },
            ToolKind.Ellipse => new EllipseElement { Start = pt, End = pt },
            ToolKind.Line => new LineElement { Start = pt, End = pt },
            ToolKind.Arrow => new ArrowElement { Start = pt, End = pt },
            ToolKind.Highlight => new HighlightElement { Start = pt, End = pt },
            ToolKind.Freehand => new FreehandElement { Points = { pt } },
            ToolKind.Blur => new ObfuscateElement { Start = pt, End = pt, Mode = ObfuscateMode.Blur, Base = _img },
            ToolKind.Pixelate => new ObfuscateElement { Start = pt, End = pt, Mode = ObfuscateMode.Pixelate, Base = _img },
            _ => new RectangleElement { Start = pt, End = pt },
        };
        el.Style = Style.Clone();
        return el;
    }

    private static bool IsValid(AnnotationElement el) => el switch
    {
        FreehandElement fh => fh.Points.Count >= 2,
        _ => el.Bounds.Width >= 3 || el.Bounds.Height >= 3,
    };

    private AnnotationElement? HitTest(Point pt)
    {
        for (int i = _doc.Elements.Count - 1; i >= 0; i--)
            if (_doc.Elements[i].HitTest(pt)) return _doc.Elements[i];
        return null;
    }

    private ScreenRect NormalizedImageRect(Point a, Point b)
    {
        int x = (int)Math.Round(Math.Min(a.X, b.X));
        int y = (int)Math.Round(Math.Min(a.Y, b.Y));
        int w = (int)Math.Round(Math.Abs(a.X - b.X));
        int h = (int)Math.Round(Math.Abs(a.Y - b.Y));
        x = Math.Clamp(x, 0, _img.Width - 1);
        y = Math.Clamp(y, 0, _img.Height - 1);
        w = Math.Clamp(w, 1, _img.Width - x);
        h = Math.Clamp(h, 1, _img.Height - y);
        return new ScreenRect(x, y, w, h);
    }

    private void Refresh()
    {
        Invalidate();
        Changed?.Invoke();
    }

    // --- render (called by the surface) ---

    internal void RenderContent(DrawingContext ctx)
    {
        ctx.DrawImage(_baseBmp, new Rect(0, 0, _img.Width, _img.Height));
        foreach (var el in _doc.Elements)
        {
            if (_editingExisting && ReferenceEquals(el, _editing)) continue;
            el.Render(ctx);
        }
        _pending?.Render(ctx);

        if (_selected is not null && !ReferenceEquals(_selected, _editing))
            DrawSelection(ctx, _selected);

        if (_cropping) DrawCropOverlay(ctx);
    }

    private void DrawCropOverlay(DrawingContext ctx)
    {
        double w = _img.Width, h = _img.Height;
        double x0 = Math.Min(_cropStart.X, _cropEnd.X), y0 = Math.Min(_cropStart.Y, _cropEnd.Y);
        double cw = Math.Abs(_cropEnd.X - _cropStart.X), ch = Math.Abs(_cropEnd.Y - _cropStart.Y);
        var sel = new Rect(x0, y0, cw, ch);
        var veil = new SolidColorBrush(Color.FromArgb(0x7A, 0x00, 0x00, 0x00));
        ctx.FillRectangle(veil, new Rect(0, 0, w, sel.Y));
        ctx.FillRectangle(veil, new Rect(0, sel.Bottom, w, Math.Max(0, h - sel.Bottom)));
        ctx.FillRectangle(veil, new Rect(0, sel.Y, sel.X, sel.Height));
        ctx.FillRectangle(veil, new Rect(sel.Right, sel.Y, Math.Max(0, w - sel.Right), sel.Height));
        ctx.DrawRectangle(null, new Pen(new SolidColorBrush(Color.FromRgb(0xE0, 0x2B, 0x2B)), 1.5), sel);
    }

    private static void DrawSelection(DrawingContext ctx, AnnotationElement el)
    {
        if (el is not LineElement)
            ctx.DrawRectangle(null, SelectionPen, el.Bounds.Inflate(2));

        const double s = 3.5;
        foreach (var (_, p) in HandlesFor(el))
            ctx.DrawRectangle(HandleFill, HandlePen, new Rect(p.X - s, p.Y - s, s * 2, s * 2));
    }

    /// <summary>Resize handles for an element: endpoints for lines/arrows, 8 box handles for shapes.</summary>
    private static IEnumerable<(HandleId id, Point p)> HandlesFor(AnnotationElement el)
    {
        if (el is LineElement line)
        {
            yield return (HandleId.P1, line.Start);
            yield return (HandleId.P2, line.End);
        }
        else if (el is TwoPointElement)
        {
            var b = el.Bounds;
            yield return (HandleId.TopLeft, b.TopLeft);
            yield return (HandleId.Top, new Point(b.Center.X, b.Y));
            yield return (HandleId.TopRight, b.TopRight);
            yield return (HandleId.Right, new Point(b.Right, b.Center.Y));
            yield return (HandleId.BottomRight, b.BottomRight);
            yield return (HandleId.Bottom, new Point(b.Center.X, b.Bottom));
            yield return (HandleId.BottomLeft, b.BottomLeft);
            yield return (HandleId.Left, new Point(b.X, b.Center.Y));
        }
        // Freehand / Text / Counter: move only (no resize handles).
    }

    private static HandleId HandleAt(Point pt, AnnotationElement el)
    {
        const double r = 7;
        foreach (var (id, p) in HandlesFor(el))
            if (Math.Abs(pt.X - p.X) <= r && Math.Abs(pt.Y - p.Y) <= r) return id;
        return HandleId.None;
    }

    private static void ApplyResize(AnnotationElement el, HandleId handle, Point pt)
    {
        if (el is LineElement line)
        {
            if (handle == HandleId.P1) line.Start = pt;
            else if (handle == HandleId.P2) line.End = pt;
            return;
        }
        if (el is TwoPointElement tp)
        {
            var b = el.Bounds;
            double l = b.X, t = b.Y, rr = b.Right, bb = b.Bottom;
            switch (handle)
            {
                case HandleId.TopLeft: l = pt.X; t = pt.Y; break;
                case HandleId.Top: t = pt.Y; break;
                case HandleId.TopRight: rr = pt.X; t = pt.Y; break;
                case HandleId.Right: rr = pt.X; break;
                case HandleId.BottomRight: rr = pt.X; bb = pt.Y; break;
                case HandleId.Bottom: bb = pt.Y; break;
                case HandleId.BottomLeft: l = pt.X; bb = pt.Y; break;
                case HandleId.Left: l = pt.X; break;
            }
            tp.SetBounds(new Rect(Math.Min(l, rr), Math.Min(t, bb), Math.Abs(rr - l), Math.Abs(bb - t)));
        }
    }

    // --- export ---

    /// <summary>Flatten base + annotations to a PNG and a CapturedImage (for the clipboard).</summary>
    public (CapturedImage image, byte[] png) Flatten()
    {
        CommitTextEdit();

        int w = _img.Width, h = _img.Height;
        var rtb = new RenderTargetBitmap(new PixelSize(w, h), new Vector(96, 96));
        using (var ctx = rtb.CreateDrawingContext())
        {
            ctx.DrawImage(_baseBmp, new Rect(0, 0, w, h));
            foreach (var el in _doc.Elements) el.Render(ctx);
        }

        byte[] png = AvaloniaImageInterop.EncodePng(rtb);

        var buf = new byte[(long)w * h * 4];
        var gch = GCHandle.Alloc(buf, GCHandleType.Pinned);
        try { rtb.CopyPixels(new PixelRect(0, 0, w, h), gch.AddrOfPinnedObject(), buf.Length, w * 4); }
        finally { gch.Free(); }

        var img = new CapturedImage(w, h, buf, _img.DpiScale, _img.OriginX, _img.OriginY);
        img.ForceOpaque();
        return (img, png);
    }

    /// <summary>The private custom-drawn surface (Control.Render is overridable; Panel.Render is not).</summary>
    private sealed class RenderSurface : Control
    {
        private readonly EditorCanvas _owner;
        public RenderSurface(EditorCanvas owner) => _owner = owner;
        public override void Render(DrawingContext ctx) => _owner.RenderContent(ctx);
    }
}
