using System;
using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace Prism;

internal static class ReticleDrawing
{
    internal static SolidColorBrush Brush(string hex)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        brush.Freeze();
        return brush;
    }

    internal static FormattedText Text(string label, double fontSize, Brush brush) =>
        new(label, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
            new Typeface("Cascadia Mono, Consolas"), fontSize, brush, 1);

    internal static void Draw(DrawingContext dc, Reticle reticle, Point center, double scaleX, double scaleY)
    {
        var fill = Brush(reticle.Color);
        var outline = reticle.Outline && reticle.OutlineOpacity > 0 ? Brush(reticle.OutlineColor) : null;
        var hasDot = reticle.CenterDot || reticle.Shape == ReticleShape.Dot;
        dc.PushTransform(new TranslateTransform(center.X, center.Y));
        dc.PushTransform(new ScaleTransform(scaleX, scaleY));
        dc.PushOpacity(reticle.Opacity / 100.0);

        if (outline is not null)
        {
            dc.PushOpacity(reticle.OutlineOpacity / 100.0);
            DrawBody(dc, reticle, outline, reticle.OutlineWidth);
            if (hasDot)
            {
                dc.PushOpacity(reticle.DotOpacity / 100.0);
                var dot = DotGeometry(reticle, reticle.DotSize / 2.0);
                var outside = DotGeometry(reticle, reticle.DotSize / 2.0 + reticle.OutlineWidth);
                dc.DrawGeometry(outline, null, new CombinedGeometry(GeometryCombineMode.Exclude, outside, dot));
                dc.Pop();
            }
            dc.Pop();
        }
        DrawBody(dc, reticle, fill, 0);

        if (hasDot)
        {
            dc.PushOpacity(reticle.DotOpacity / 100.0);
            var dotFill = reticle.DotColor is { } dotColor ? Brush(dotColor) : fill;
            dc.DrawGeometry(dotFill, null, DotGeometry(reticle, reticle.DotSize / 2.0));
            dc.Pop();
        }

        dc.Pop();
        dc.Pop();
        dc.Pop();
    }

    private static void DrawBody(DrawingContext dc, Reticle r, Brush fill, double padding)
    {
        if (r.Shape == ReticleShape.Ring)
        {
            var pen = new Pen(fill, r.Thickness + padding * 2);
            double radius = Math.Max(2, r.Length / 2.0);
            dc.DrawEllipse(null, pen, new Point(), radius, radius);
        }

        if (r.Shape is ReticleShape.Cross or ReticleShape.TShape or ReticleShape.Diagonal)
        {
            double halfWidth = r.Thickness / 2.0 + padding;
            var diagonal = r.Shape == ReticleShape.Diagonal ? 45 : 0;
            for (var side = 0; side < 4; side++)
            {
                var armFlag = (ReticleArms)(1 << side);
                if ((r.VisibleArms & armFlag) == 0 || (r.Shape == ReticleShape.TShape && armFlag == ReticleArms.Top))
                    continue;

                var vertical = side % 2 == 1;
                int length = vertical ? r.VerticalLength ?? r.Length : r.Length;
                int gap = vertical ? r.VerticalGap ?? r.Gap : r.Gap;
                var arm = new Rect(gap - padding, -halfWidth, length + padding * 2, halfWidth * 2);
                double radius = r.RoundedEnds ? halfWidth : 0;
                dc.PushTransform(new RotateTransform(r.Rotation + diagonal + 90 * side));
                dc.DrawRoundedRectangle(fill, null, arm, radius, radius);
                dc.Pop();
            }
        }

        if (r.Shape is ReticleShape.Chevron or ReticleShape.Brackets or ReticleShape.Cat)
        {
            dc.PushTransform(new RotateTransform(r.Rotation));
            var cap = r.RoundedEnds ? PenLineCap.Round : PenLineCap.Square;
            var pen = new Pen(fill, r.Thickness + padding * 2)
            {
                StartLineCap = cap, EndLineCap = cap,
                LineJoin = r.RoundedEnds ? PenLineJoin.Round : PenLineJoin.Bevel
            };
            if (r.Shape == ReticleShape.Chevron)
            {
                double height = r.Length * 0.65;
                DrawAngle(dc, pen, new Point(-r.Length, height - r.Gap), new Point(0, -r.Gap),
                    new Point(r.Length, height - r.Gap));
            }
            else if (r.Shape == ReticleShape.Cat)
                DrawCat(dc, pen, r);
            else
            {
                double depth = r.Length / 2.0;
                double edge = r.Gap + depth;
                DrawAngle(dc, pen, new Point(-edge + depth, -edge), new Point(-edge, -edge), new Point(-edge, -edge + depth));
                DrawAngle(dc, pen, new Point(edge - depth, -edge), new Point(edge, -edge), new Point(edge, -edge + depth));
                DrawAngle(dc, pen, new Point(-edge + depth, edge), new Point(-edge, edge), new Point(-edge, edge - depth));
                DrawAngle(dc, pen, new Point(edge - depth, edge), new Point(edge, edge), new Point(edge, edge - depth));
            }
            dc.Pop();
        }
    }

    private static void DrawCat(DrawingContext dc, Pen pen, Reticle reticle)
    {
        double face = reticle.Length / 2.0;
        double rim = Math.Max(3, reticle.Gap + reticle.Thickness / 2.0);
        double ears = rim + face * 0.68;
        var silhouette = new StreamGeometry();
        using (var points = silhouette.Open())
        {
            points.BeginFigure(new Point(-face, rim * -0.35), false, false);
            points.LineTo(new Point(-face, -ears), true, false);
            points.LineTo(new Point(-face * 0.36, -rim), true, false);
            points.LineTo(new Point(face * 0.36, -rim), true, false);
            points.LineTo(new Point(face, -ears), true, false);
            points.LineTo(new Point(face, rim * -0.35), true, false);
            points.BeginFigure(new Point(-face, rim * 0.4), false, false);
            points.QuadraticBezierTo(new Point(-face * 0.92, rim + face * 0.48), new Point(0, rim + face * 0.48), true, true);
            points.QuadraticBezierTo(new Point(face * 0.92, rim + face * 0.48), new Point(face, rim * 0.4), true, true);
        }
        dc.DrawGeometry(null, pen, silhouette);
    }

    private static void DrawAngle(DrawingContext dc, Pen pen, Point start, Point bend, Point end)
    {
        var geometry = new StreamGeometry();
        using (var points = geometry.Open())
        {
            points.BeginFigure(start, isFilled: false, isClosed: false);
            points.LineTo(bend, isStroked: true, isSmoothJoin: false);
            points.LineTo(end, isStroked: true, isSmoothJoin: false);
        }
        dc.DrawGeometry(null, pen, geometry);
    }

    private static Geometry DotGeometry(Reticle r, double radius) =>
        r.SquareDot
            ? new RectangleGeometry(new Rect(-radius, -radius, radius * 2, radius * 2))
            : new EllipseGeometry(new Point(), radius, radius);
}

public sealed class PresetGlyph : FrameworkElement
{
    private Reticle _reticle = new();

    internal Reticle Reticle
    {
        set
        {
            _reticle = value;
            InvalidateVisual();
        }
    }

    protected override void OnRender(DrawingContext dc) =>
        ReticleDrawing.Draw(dc, _reticle, new Point(ActualWidth / 2, ActualHeight / 2), 0.82, 0.82);
}

internal enum PreviewScene { Contrast, Grid, Light, Dark, Split }

public sealed class PreviewSurface : FrameworkElement
{
    private static readonly Brush DotBrush = ReticleDrawing.Brush("#3D5356");
    private static readonly Brush LightDotBrush = ReticleDrawing.Brush("#B3BCD2");
    private static readonly Brush LabelBrush = ReticleDrawing.Brush("#91A4A7");
    private static readonly Brush LightLabelBrush = ReticleDrawing.Brush("#515A75");
    private static readonly Pen Rule = new(ReticleDrawing.Brush("#33474B"), 1);
    private static readonly Pen LightRule = new(ReticleDrawing.Brush("#BBC5DA"), 1);
    private static readonly Brush Dark = ReticleDrawing.Brush("#101519");
    private static readonly Brush Light = ReticleDrawing.Brush("#DDE8E7");
    private static readonly Brush Gradient = new RadialGradientBrush(
        (Color)ColorConverter.ConvertFromString("#253236"), (Color)ColorConverter.ConvertFromString("#181F23"))
    {
        RadiusX = 0.58, RadiusY = 0.79
    };

    private Reticle _reticle = new();
    private PreviewScene _scene;
    private int _zoom = 4;

    private bool _guides = true;
    private Reticle? _comparison;

    internal void SetDisplayOptions(bool guides, Reticle? comparison)
    {
        _guides = guides;
        _comparison = comparison;
        InvalidateVisual();
    }

    internal void Refresh(Reticle reticle, PreviewScene scene, int zoom)
    {
        _reticle = reticle;
        _scene = scene;
        _zoom = zoom;
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext dc)
    {
        var bounds = new Rect(RenderSize);
        var center = new Point(bounds.Width / 2, bounds.Height / 2);
        var isLight = _scene == PreviewScene.Light;
        var pen = isLight ? LightRule : Rule;
        var text = isLight ? LightLabelBrush : LabelBrush;
        dc.PushClip(new RectangleGeometry(bounds));
        if (_scene == PreviewScene.Contrast)
            ContrastScene.Draw(dc, RenderSize);
        else if (_scene == PreviewScene.Split)
        {
            if (_comparison is not null)
            {
                DrawSplit(dc, new Rect(0, 0, center.X, bounds.Height));
                DrawSplit(dc, new Rect(center.X, 0, center.X, bounds.Height));
            }
            else
                DrawSplit(dc, bounds);
        }
        else
            dc.DrawRectangle(_scene switch { PreviewScene.Light => Light, PreviewScene.Dark => Dark, _ => Gradient }, null, bounds);

        if (_guides && _comparison is null && _scene is (PreviewScene.Grid or PreviewScene.Light))
        {
            for (var x = center.X % 20; x < bounds.Width; x += 20)
            for (var y = center.Y % 20; y < bounds.Height; y += 20)
                dc.DrawEllipse(isLight ? LightDotBrush : DotBrush, null, new Point(x, y), 0.85, 0.85);

            if (_scene == PreviewScene.Grid)
                DrawRulers(dc, bounds, center);
            else
                dc.DrawEllipse(null, pen, center, 72, 72);
            dc.DrawLine(pen, new Point(center.X, 28), new Point(center.X, bounds.Bottom - 28));
            dc.DrawLine(pen, new Point(28, center.Y), new Point(bounds.Right - 28, center.Y));
            dc.DrawText(ReticleDrawing.Text("−X", 10, text), new Point(25, center.Y + 7));
            dc.DrawText(ReticleDrawing.Text("+X", 10, text), new Point(bounds.Right - 28, center.Y + 7));
            dc.DrawText(ReticleDrawing.Text("−Y", 10, text), new Point(center.X + 7, 25));
            dc.DrawText(ReticleDrawing.Text("+Y", 10, text), new Point(center.X + 7, bounds.Bottom - 25));
        }

        if (_comparison is { } previous)
        {
            DrawComparisonPane(dc, new Rect(0, 0, center.X, bounds.Height), previous, "До");
            DrawComparisonPane(dc, new Rect(center.X, 0, center.X, bounds.Height), _reticle, "Сейчас");
            dc.DrawLine(Rule, new Point(center.X, 0), new Point(center.X, bounds.Height));
        }
        else
            DrawPreviewReticle(dc, _reticle, center);
        dc.Pop();
    }

    private static void DrawSplit(DrawingContext dc, Rect bounds)
    {
        var half = bounds.Width / 2;
        dc.DrawRectangle(Dark, null, new Rect(bounds.Left, bounds.Top, half, bounds.Height));
        dc.DrawRectangle(Light, null, new Rect(bounds.Left + half, bounds.Top, half, bounds.Height));
    }

    private void DrawComparisonPane(DrawingContext dc, Rect bounds, Reticle reticle, string label)
    {
        dc.PushClip(new RectangleGeometry(bounds));
        DrawPreviewReticle(dc, reticle, new Point(bounds.Left + bounds.Width / 2, bounds.Height / 2));
        var description = ReticleDrawing.Text(label, 12, Brushes.White);
        dc.DrawRoundedRectangle(Dark, null, new Rect(bounds.Left + 13, 13, description.Width + 16, 23), 5, 5);
        dc.DrawText(description, new Point(bounds.Left + 21, 18));
        dc.Pop();
    }

    private void DrawPreviewReticle(DrawingContext dc, Reticle reticle, Point center)
    {
        var dpi = VisualTreeHelper.GetDpi(this);
        var scaleX = _zoom / dpi.DpiScaleX;
        var scaleY = _zoom / dpi.DpiScaleY;
        var position = new Point(center.X + reticle.OffsetX * scaleX, center.Y + reticle.OffsetY * scaleY);
        ReticleDrawing.Draw(dc, reticle, position, scaleX, scaleY);
    }

    private void DrawRulers(DrawingContext dc, Rect bounds, Point center)
    {
        const int edge = 20;
        var strip = ReticleDrawing.Brush("#202A2D");
        dc.DrawRectangle(strip, null, new Rect(0, 0, bounds.Width, edge));
        dc.DrawRectangle(strip, null, new Rect(0, 0, edge, bounds.Height));
        var dpi = VisualTreeHelper.GetDpi(this);
        var step = 10 * _zoom / dpi.DpiScaleX;
        for (var index = (int)Math.Ceiling((edge - center.X) / step); center.X + index * step < bounds.Width; index++)
        {
            var x = center.X + index * step;
            var major = index % 5 == 0;
            dc.DrawLine(Rule, new Point(x, major ? 11 : 15), new Point(x, edge));
            if (major && x < bounds.Width - 24)
                dc.DrawText(ReticleDrawing.Text((index * 10).ToString(), 9, LabelBrush), new Point(x + 3, 1));
        }
        step = 10 * _zoom / dpi.DpiScaleY;
        for (var index = (int)Math.Ceiling((edge - center.Y) / step); center.Y + index * step < bounds.Height; index++)
        {
            var y = center.Y + index * step;
            dc.DrawLine(Rule, new Point(index % 5 == 0 ? 10 : 15, y), new Point(edge, y));
        }
        dc.DrawText(ReticleDrawing.Text("px", 9, LabelBrush), new Point(4, 3));
    }
}

internal sealed class OverlayView : FrameworkElement
{
    internal Reticle Reticle { get; set; } = new();

    protected override void OnRender(DrawingContext dc)
    {
        var dpi = VisualTreeHelper.GetDpi(this);
        ReticleDrawing.Draw(dc, Reticle, new Point(ActualWidth / 2, ActualHeight / 2),
            1 / dpi.DpiScaleX, 1 / dpi.DpiScaleY);
    }
}
