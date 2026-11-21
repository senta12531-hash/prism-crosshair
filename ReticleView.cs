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
        dc.PushTransform(new TranslateTransform(center.X, center.Y));
        dc.PushTransform(new ScaleTransform(scaleX, scaleY));
        dc.PushOpacity(reticle.Opacity / 100.0);

        if (reticle.Outline)
            DrawShape(dc, reticle, Brush(reticle.OutlineColor), reticle.OutlineWidth);
        DrawShape(dc, reticle, fill, 0);

        dc.Pop();
        dc.Pop();
        dc.Pop();
    }

    private static void DrawShape(DrawingContext dc, Reticle r, Brush fill, double padding)
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
                if (r.Shape == ReticleShape.TShape && side == 3)
                    continue;

                int length = side % 2 == 1 ? r.VerticalLength ?? r.Length : r.Length;
                var arm = new Rect(r.Gap - padding, -halfWidth, length + padding * 2, halfWidth * 2);
                double radius = r.RoundedEnds ? halfWidth : 0;
                dc.PushTransform(new RotateTransform(r.Rotation + diagonal + 90 * side));
                dc.DrawRoundedRectangle(fill, null, arm, radius, radius);
                dc.Pop();
            }
        }

        if (r.CenterDot || r.Shape == ReticleShape.Dot)
        {
            double radius = r.DotSize / 2.0 + padding;
            if (r.SquareDot)
                dc.DrawRectangle(fill, null, new Rect(-radius, -radius, radius * 2, radius * 2));
            else
                dc.DrawEllipse(fill, null, new Point(), radius, radius);
        }
    }
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

internal enum PreviewScene { Contrast, Grid, Light, Dark }

public sealed class PreviewSurface : FrameworkElement
{
    private static readonly Brush DotBrush = ReticleDrawing.Brush("#424544");
    private static readonly Brush LightDotBrush = ReticleDrawing.Brush("#B3BCD2");
    private static readonly Brush LabelBrush = ReticleDrawing.Brush("#90958F");
    private static readonly Brush LightLabelBrush = ReticleDrawing.Brush("#515A75");
    private static readonly Pen Rule = new(ReticleDrawing.Brush("#333635"), 1);
    private static readonly Pen LightRule = new(ReticleDrawing.Brush("#BBC5DA"), 1);
    private static readonly Brush Dark = ReticleDrawing.Brush("#090A10");
    private static readonly Brush Light = ReticleDrawing.Brush("#DCE2F1");
    private static readonly Brush Gradient = new RadialGradientBrush(
        (Color)ColorConverter.ConvertFromString("#252725"), (Color)ColorConverter.ConvertFromString("#1A1C1B"))
    {
        RadiusX = 0.58, RadiusY = 0.79
    };

    private Reticle _reticle = new();
    private PreviewScene _scene;
    private int _zoom = 4;

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
        else
            dc.DrawRectangle(_scene switch { PreviewScene.Light => Light, PreviewScene.Dark => Dark, _ => Gradient }, null, bounds);

        if (_scene is PreviewScene.Grid or PreviewScene.Light)
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

        var drawnAt = new Point(center.X + _reticle.OffsetX * _zoom, center.Y + _reticle.OffsetY * _zoom);
        ReticleDrawing.Draw(dc, _reticle, drawnAt, _zoom, _zoom);
        dc.Pop();
    }

    private void DrawRulers(DrawingContext dc, Rect bounds, Point center)
    {
        const int edge = 20;
        var strip = ReticleDrawing.Brush("#202220");
        dc.DrawRectangle(strip, null, new Rect(0, 0, bounds.Width, edge));
        dc.DrawRectangle(strip, null, new Rect(0, 0, edge, bounds.Height));
        var step = 10 * _zoom;
        for (var index = (int)Math.Ceiling((edge - center.X) / step); center.X + index * step < bounds.Width; index++)
        {
            var x = center.X + index * step;
            var major = index % 5 == 0;
            dc.DrawLine(Rule, new Point(x, major ? 11 : 15), new Point(x, edge));
            if (major && x < bounds.Width - 24)
                dc.DrawText(ReticleDrawing.Text((index * 10).ToString(), 9, LabelBrush), new Point(x + 3, 1));
        }
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
