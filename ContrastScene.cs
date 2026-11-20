using System.Windows;
using System.Windows.Media;

namespace Prism;

internal static class ContrastScene
{
    private const double Width = 1000;
    private const double Height = 360;
    private static readonly DrawingGroup Art = Create();

    internal static void Draw(DrawingContext context, Size size)
    {
        context.PushTransform(new ScaleTransform(size.Width / Width, size.Height / Height));
        context.DrawDrawing(Art);
        context.Pop();
    }

    private static DrawingGroup Create()
    {
        var drawing = new DrawingGroup();
        using (var dc = drawing.Open())
        {
            var bounds = new Rect(0, 0, Width, Height);
            var backdrop = new RadialGradientBrush
            {
                Center = new Point(0.5, 0.51), GradientOrigin = new Point(0.5, 0.51),
                RadiusX = 0.61, RadiusY = 0.68,
                GradientStops = new GradientStopCollection
                {
                    Stop("#213C73", 0), Stop("#111E38", 0.62), Stop("#080C17", 1)
                }
            };
            dc.DrawRectangle(backdrop, null, bounds);

            DrawPolygon(dc, Gradient("#30476D", "#080E1B", new Point(0.5, 0), new Point(0.5, 1)),
                new Point(427, 198), new Point(573, 198), new Point(1000, 360), new Point(0, 360));
            for (var step = 0; step < 4; step++)
            {
                var y = 215 + step * step * 13;
                Line(dc, "#324668", 0.7, new Point(500 - (y - 187) * 2, y),
                    new Point(500 + (y - 187) * 2, y));
            }

            DrawPolygon(dc, Gradient("#14233F", "#253E66", new Point(0, 1), new Point(1, 0)),
                new Point(0, 0), new Point(432, 138), new Point(432, 200), new Point(0, 360));
            DrawPolygon(dc, Gradient("#253D66", "#101C32", new Point(0, 0), new Point(1, 1)),
                new Point(1000, 0), new Point(568, 138), new Point(568, 200), new Point(1000, 360));

            dc.DrawRectangle(Gradient("#223550", "#526C94", new Point(0, 0), new Point(1, 1)),
                new Pen(ReticleDrawing.Brush("#6884AF"), 0.65), new Rect(432, 126, 136, 81));
            dc.DrawRectangle(Gradient("#101D3B", "#355278", new Point(0.5, 0), new Point(0.5, 1)),
                new Pen(ReticleDrawing.Brush("#7490BD"), 0.75), new Rect(481, 145, 38, 62));

            DrawWallLines(dc, 0, 432);
            DrawWallLines(dc, 1000, 568);

            var vignette = new RadialGradientBrush
            {
                Center = new Point(0.5, 0.45), GradientOrigin = new Point(0.5, 0.45),
                RadiusX = 0.7, RadiusY = 0.73,
                GradientStops = new GradientStopCollection
                {
                    Stop("#00080B13", 0.25), Stop("#CC060911", 1)
                }
            };
            dc.DrawRectangle(vignette, null, bounds);
        }
        drawing.Freeze();
        return drawing;
    }

    private static void DrawWallLines(DrawingContext dc, double edge, double center)
    {
        Line(dc, "#5978AD", 1, new Point(edge, 5), new Point(center, 145));
        Line(dc, "#8DABE0", 0.55, new Point(edge, 11), new Point(center, 149));
        Line(dc, "#273A59", 0.9, new Point(edge, 101), new Point(center, 163));
        Line(dc, "#384D73", 0.9, new Point(edge, 204), new Point(center, 182));
        Line(dc, "#5F7AA9", 1, new Point(edge, 333), new Point(center, 198));
        Line(dc, "#7392C7", 0.55, new Point(edge, 340), new Point(center, 202));
    }

    private static GradientStop Stop(string color, double offset) =>
        new((Color)ColorConverter.ConvertFromString(color), offset);

    private static LinearGradientBrush Gradient(string start, string end, Point from, Point to) =>
        new() { StartPoint = from, EndPoint = to, GradientStops = new GradientStopCollection { Stop(start, 0), Stop(end, 1) } };

    private static void Line(DrawingContext dc, string color, double width, Point start, Point end) =>
        dc.DrawLine(new Pen(ReticleDrawing.Brush(color), width), start, end);

    private static void DrawPolygon(DrawingContext dc, Brush brush, Point first, params Point[] rest)
    {
        var shape = new StreamGeometry();
        using (var geometry = shape.Open())
        {
            geometry.BeginFigure(first, isFilled: true, isClosed: true);
            geometry.PolyLineTo(rest, isStroked: true, isSmoothJoin: false);
        }
        dc.DrawGeometry(brush, null, shape);
    }
}
