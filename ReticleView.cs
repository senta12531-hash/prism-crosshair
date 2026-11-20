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

    internal static void Draw(DrawingContext dc, Reticle reticle, Point center, double scaleX, double scaleY, bool glow)
    {
        var fill = Brush(reticle.Color);
        dc.PushTransform(new TranslateTransform(center.X, center.Y));
        dc.PushTransform(new ScaleTransform(scaleX, scaleY));
        dc.PushOpacity(reticle.Opacity / 100.0);

        if (glow)
        {
            for (var padding = 3.0; padding >= 1; padding -= 0.5)
            {
                dc.PushOpacity(0.012 * (4 - padding));
                DrawShape(dc, reticle, fill, padding);
                dc.Pop();
            }
        }
        if (reticle.Outline)
            DrawShape(dc, reticle, Brushes.Black, reticle.OutlineWidth);
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
            var arm = new Rect(r.Gap - padding, -halfWidth, r.Length + padding * 2, halfWidth * 2);
            var diagonal = r.Shape == ReticleShape.Diagonal ? 45 : 0;
            for (var side = 0; side < 4; side++)
            {
                if (r.Shape == ReticleShape.TShape && side == 3)
                    continue;

                dc.PushTransform(new RotateTransform(diagonal + 90 * side));
                dc.DrawRectangle(fill, null, arm);
                dc.Pop();
            }
        }

        if (r.CenterDot || r.Shape == ReticleShape.Dot)
        {
            double radius = r.DotSize / 2.0 + padding;
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
        ReticleDrawing.Draw(dc, _reticle, new Point(ActualWidth / 2, ActualHeight / 2), 0.82, 0.82, false);
}

public sealed class PreviewSurface : FrameworkElement
{
    private static readonly Brush DotBrush = ReticleDrawing.Brush("#37374E");
    private static readonly Brush LightDotBrush = ReticleDrawing.Brush("#B3BCD2");
    private static readonly Brush LabelBrush = ReticleDrawing.Brush("#777993");
    private static readonly Brush LightLabelBrush = ReticleDrawing.Brush("#515A75");
    private static readonly Pen Rule = new(ReticleDrawing.Brush("#34354C"), 1);
    private static readonly Pen LightRule = new(ReticleDrawing.Brush("#BBC5DA"), 1);
    private static readonly Brush Dark = ReticleDrawing.Brush("#090A10");
    private static readonly Brush Light = ReticleDrawing.Brush("#DCE2F1");
    private static readonly Brush Gradient = new RadialGradientBrush(
        (Color)ColorConverter.ConvertFromString("#24233A"), (Color)ColorConverter.ConvertFromString("#141521"))
    {
        RadiusX = 0.58, RadiusY = 0.79
    };

    private Reticle _reticle = new();
    private int _scene;
    private int _zoom = 4;

    internal void Refresh(Reticle reticle, int scene, int zoom)
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
        var isLight = _scene == 1;
        var pen = isLight ? LightRule : Rule;
        var text = isLight ? LightLabelBrush : LabelBrush;
        dc.DrawRectangle(_scene switch { 1 => Light, 2 => Dark, _ => Gradient }, null, bounds);
        dc.PushClip(new RectangleGeometry(bounds));

        if (_scene != 2)
        {
            for (var x = center.X % 20; x < bounds.Width; x += 20)
            for (var y = center.Y % 20; y < bounds.Height; y += 20)
                dc.DrawEllipse(isLight ? LightDotBrush : DotBrush, null, new Point(x, y), 0.85, 0.85);

            dc.DrawEllipse(null, pen, center, 72, 72);
            dc.DrawEllipse(null, pen, center, 112, 112);
            dc.DrawLine(pen, new Point(center.X, 28), new Point(center.X, bounds.Bottom - 28));
            dc.DrawLine(pen, new Point(28, center.Y), new Point(bounds.Right - 28, center.Y));
            dc.DrawText(ReticleDrawing.Text("−X", 10, text), new Point(15, center.Y + 7));
            dc.DrawText(ReticleDrawing.Text("+X", 10, text), new Point(bounds.Right - 28, center.Y + 7));
            dc.DrawText(ReticleDrawing.Text("−Y", 10, text), new Point(center.X + 7, 13));
            dc.DrawText(ReticleDrawing.Text("+Y", 10, text), new Point(center.X + 7, bounds.Bottom - 25));
        }

        var drawnAt = new Point(center.X + _reticle.OffsetX * _zoom, center.Y + _reticle.OffsetY * _zoom);
        ReticleDrawing.Draw(dc, _reticle, drawnAt, _zoom, _zoom, _scene == 0);
        dc.Pop();
    }
}

internal sealed class OverlayView : FrameworkElement
{
    internal Reticle Reticle { get; set; } = new();

    protected override void OnRender(DrawingContext dc)
    {
        var dpi = VisualTreeHelper.GetDpi(this);
        ReticleDrawing.Draw(dc, Reticle, new Point(ActualWidth / 2, ActualHeight / 2),
            1 / dpi.DpiScaleX, 1 / dpi.DpiScaleY, false);
    }
}
