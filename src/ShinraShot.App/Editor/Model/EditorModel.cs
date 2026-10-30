// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Shinra CC

using System.Globalization;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using ShinraShot.App.Imaging;
using ShinraShot.Core.Imaging;

namespace ShinraShot.App.Editor.Model;

/// <summary>The active editor tool.</summary>
public enum ToolKind
{
    Select,
    Rectangle,
    Ellipse,
    Line,
    Arrow,
    Freehand,
    Text,
    Highlight,
    Counter,
    Blur,
    Pixelate,
    Crop,
}

/// <summary>Which obfuscation an <see cref="ObfuscateElement"/> applies.</summary>
public enum ObfuscateMode { Blur, Pixelate }

/// <summary>Stroke/fill/font styling shared by the toolbar and each element.</summary>
public sealed class ToolStyle
{
    public Color StrokeColor { get; set; } = Color.FromRgb(0xE0, 0x2B, 0x2B);
    public double StrokeThickness { get; set; } = 3;
    public bool Fill { get; set; }
    public Color FillColor { get; set; } = Color.FromArgb(0x40, 0xE0, 0x2B, 0x2B);
    public double FontSize { get; set; } = 20;

    public ToolStyle Clone() => (ToolStyle)MemberwiseClone();

    public IPen StrokePen() => new Pen(new SolidColorBrush(StrokeColor), StrokeThickness,
        lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);

    public IBrush? FillBrush() => Fill ? new SolidColorBrush(FillColor) : null;

    public IBrush StrokeBrush() => new SolidColorBrush(StrokeColor);
}

/// <summary>Base class for every annotation drawn over the capture.</summary>
public abstract class AnnotationElement
{
    public ToolStyle Style { get; set; } = new();

    public abstract void Render(DrawingContext ctx);
    public abstract Rect Bounds { get; }
    public abstract void Move(double dx, double dy);
    public abstract AnnotationElement Clone();

    /// <summary>Default hit test: within a small inflation of the bounds.</summary>
    public virtual bool HitTest(Point p) => Bounds.Inflate(6).Contains(p);

    protected static Rect RectFrom(Point a, Point b)
        => new(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y));
}

/// <summary>Shapes defined by two drag points (rect, ellipse, line, arrow).</summary>
public abstract class TwoPointElement : AnnotationElement
{
    public Point Start { get; set; }
    public Point End { get; set; }

    public override Rect Bounds => RectFrom(Start, End);

    public override void Move(double dx, double dy)
    {
        var v = new Vector(dx, dy);
        Start += v;
        End += v;
    }

    /// <summary>Set the element to span the given rectangle (top-left → bottom-right).</summary>
    public void SetBounds(Rect r)
    {
        Start = new Point(r.X, r.Y);
        End = new Point(r.Right, r.Bottom);
    }

    protected T CopyTo<T>(T clone) where T : TwoPointElement
    {
        clone.Start = Start;
        clone.End = End;
        clone.Style = Style.Clone();
        return clone;
    }
}

public sealed class RectangleElement : TwoPointElement
{
    public override void Render(DrawingContext ctx)
        => ctx.DrawRectangle(Style.FillBrush(), Style.StrokePen(), RectFrom(Start, End));

    public override AnnotationElement Clone() => CopyTo(new RectangleElement());
}

public sealed class EllipseElement : TwoPointElement
{
    public override void Render(DrawingContext ctx)
    {
        var r = RectFrom(Start, End);
        ctx.DrawEllipse(Style.FillBrush(), Style.StrokePen(), r.Center, r.Width / 2, r.Height / 2);
    }

    public override AnnotationElement Clone() => CopyTo(new EllipseElement());
}

public class LineElement : TwoPointElement
{
    public override void Render(DrawingContext ctx) => ctx.DrawLine(Style.StrokePen(), Start, End);

    // Distance from the segment, so thin diagonal lines are still easy to grab.
    public override bool HitTest(Point p) => DistanceToSegment(p, Start, End) <= Style.StrokeThickness + 6;

    public override AnnotationElement Clone() => CopyTo(new LineElement());

    protected static double DistanceToSegment(Point p, Point a, Point b)
    {
        double abx = b.X - a.X, aby = b.Y - a.Y;
        double len2 = abx * abx + aby * aby;
        if (len2 < 1e-6) return Dist(p.X, p.Y, a.X, a.Y);
        double t = Math.Clamp(((p.X - a.X) * abx + (p.Y - a.Y) * aby) / len2, 0, 1);
        return Dist(p.X, p.Y, a.X + abx * t, a.Y + aby * t);
    }

    private static double Dist(double x1, double y1, double x2, double y2)
    {
        double dx = x1 - x2, dy = y1 - y2;
        return Math.Sqrt(dx * dx + dy * dy);
    }
}

public sealed class ArrowElement : LineElement
{
    public override void Render(DrawingContext ctx)
    {
        ctx.DrawLine(Style.StrokePen(), Start, End);

        var dir = new Vector(End.X - Start.X, End.Y - Start.Y);
        double len = dir.Length;
        if (len < 0.001) return;

        var u = dir / len;
        var perp = new Vector(-u.Y, u.X);
        double ah = Math.Max(10, Style.StrokeThickness * 3.2);
        var b1 = End - u * ah + perp * (ah * 0.55);
        var b2 = End - u * ah - perp * (ah * 0.55);

        var geo = new StreamGeometry();
        using (var g = geo.Open())
        {
            g.BeginFigure(End, true);
            g.LineTo(b1);
            g.LineTo(b2);
            g.EndFigure(true);
        }
        ctx.DrawGeometry(Style.StrokeBrush(), null, geo);
    }

    public override AnnotationElement Clone() => CopyTo(new ArrowElement());
}

public sealed class FreehandElement : AnnotationElement
{
    public List<Point> Points { get; set; } = new();

    public override Rect Bounds
    {
        get
        {
            if (Points.Count == 0) return default;
            double minX = double.MaxValue, minY = double.MaxValue, maxX = double.MinValue, maxY = double.MinValue;
            foreach (var p in Points)
            {
                minX = Math.Min(minX, p.X); minY = Math.Min(minY, p.Y);
                maxX = Math.Max(maxX, p.X); maxY = Math.Max(maxY, p.Y);
            }
            return new Rect(minX, minY, maxX - minX, maxY - minY);
        }
    }

    public override void Render(DrawingContext ctx)
    {
        if (Points.Count < 2) return;
        var geo = new StreamGeometry();
        using (var g = geo.Open())
        {
            g.BeginFigure(Points[0], false);
            for (int i = 1; i < Points.Count; i++) g.LineTo(Points[i]);
            g.EndFigure(false);
        }
        ctx.DrawGeometry(null, Style.StrokePen(), geo);
    }

    public override void Move(double dx, double dy)
    {
        var v = new Vector(dx, dy);
        for (int i = 0; i < Points.Count; i++) Points[i] += v;
    }

    public override AnnotationElement Clone()
        => new FreehandElement { Points = new List<Point>(Points), Style = Style.Clone() };
}

/// <summary>A highlighter rectangle: a translucent fill over the region (no border).</summary>
public sealed class HighlightElement : TwoPointElement
{
    public override void Render(DrawingContext ctx)
    {
        var c = Style.StrokeColor;
        var brush = new SolidColorBrush(Color.FromArgb(0x55, c.R, c.G, c.B));
        ctx.FillRectangle(brush, RectFrom(Start, End));
    }

    public override AnnotationElement Clone() => CopyTo(new HighlightElement());
}

/// <summary>A text annotation anchored at its top-left, colored by the stroke color.</summary>
public sealed class TextElement : AnnotationElement
{
    public Point Position { get; set; }
    public string Text { get; set; } = "";

    private FormattedText Build() => new(
        Text.Length == 0 ? " " : Text,
        CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
        new Typeface("Segoe UI"), Style.FontSize, Style.StrokeBrush());

    public override Rect Bounds
    {
        get { var ft = Build(); return new Rect(Position, new Size(ft.Width, ft.Height)); }
    }

    public override void Render(DrawingContext ctx)
    {
        if (Text.Length == 0) return;
        ctx.DrawText(Build(), Position);
    }

    public override void Move(double dx, double dy) => Position += new Vector(dx, dy);

    public override AnnotationElement Clone()
        => new TextElement { Position = Position, Text = Text, Style = Style.Clone() };
}

/// <summary>An auto-numbered step marker: a filled circle with a white number.</summary>
public sealed class CounterElement : AnnotationElement
{
    public Point Center { get; set; }
    public int Number { get; set; } = 1;

    private double Radius => Math.Max(12, Style.FontSize * 0.85 + Style.StrokeThickness);

    public override Rect Bounds => new(Center.X - Radius, Center.Y - Radius, Radius * 2, Radius * 2);

    public override void Render(DrawingContext ctx)
    {
        ctx.DrawEllipse(Style.StrokeBrush(), null, Center, Radius, Radius);
        var ft = new FormattedText(Number.ToString(CultureInfo.InvariantCulture),
            CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
            new Typeface("Segoe UI", FontStyle.Normal, FontWeight.Bold), Radius * 1.05, Brushes.White);
        ctx.DrawText(ft, new Point(Center.X - ft.Width / 2, Center.Y - ft.Height / 2));
    }

    public override void Move(double dx, double dy) => Center += new Vector(dx, dy);

    public override AnnotationElement Clone()
        => new CounterElement { Center = Center, Number = Number, Style = Style.Clone() };
}

/// <summary>
/// Obfuscates a region of the base capture (blur or pixelate). Holds a shared, read-only
/// reference to the base image and caches the processed tile until its rect/mode changes.
/// </summary>
public sealed class ObfuscateElement : TwoPointElement
{
    public ObfuscateMode Mode { get; set; } = ObfuscateMode.Blur;
    public CapturedImage Base { get; set; } = null!;

    private Rect _cacheRect;
    private ObfuscateMode _cacheMode;
    private WriteableBitmap? _cache;

    public override void Render(DrawingContext ctx)
    {
        if (Base is null) return;
        var r = RectFrom(Start, End);
        int rx = Math.Clamp((int)r.X, 0, Base.Width - 1);
        int ry = Math.Clamp((int)r.Y, 0, Base.Height - 1);
        int rw = Math.Clamp((int)r.Width, 1, Base.Width - rx);
        int rh = Math.Clamp((int)r.Height, 1, Base.Height - ry);
        var clamped = new Rect(rx, ry, rw, rh);

        if (_cache is null || _cacheRect != clamped || _cacheMode != Mode)
        {
            var region = Base.Crop(rx, ry, rw, rh);
            var proc = Mode == ObfuscateMode.Pixelate
                ? ImageEffects.Pixelate(region, Math.Max(6, (int)(Style.StrokeThickness * 3)))
                : ImageEffects.Blur(region, Math.Max(3, (int)Style.StrokeThickness));
            _cache = AvaloniaImageInterop.ToWriteableBitmap(proc);
            _cacheRect = clamped;
            _cacheMode = Mode;
        }

        ctx.DrawImage(_cache, new Rect(rx, ry, rw, rh));
    }

    public override AnnotationElement Clone()
    {
        var c = CopyTo(new ObfuscateElement());
        c.Mode = Mode;
        c.Base = Base;
        return c;
    }
}
