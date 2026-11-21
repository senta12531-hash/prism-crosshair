using System.Windows;
using System.Windows.Media;

namespace Prism;

public sealed class PixelGuide : FrameworkElement
{
    private static readonly Brush Background = ReticleDrawing.Brush("#273A3B");
    private static readonly Brush Fur = ReticleDrawing.Brush("#A9DECF");
    private static readonly Brush InnerEar = ReticleDrawing.Brush("#F0B6BF");
    private static readonly Brush Dark = ReticleDrawing.Brush("#223536");
    private static readonly Brush Light = ReticleDrawing.Brush("#D6F4EC");
    private bool _excited;
    private static readonly Geometry Heart = Geometry.Parse("M34,12 C32,10 28,8 28,5 C28,1 33,0 34,4 C36,0 40,1 40,5 C40,8 36,10 34,12 Z");
    private bool _showHeart;

    internal bool ShowHeart
    {
        set
        {
            _showHeart = value;
            InvalidateVisual();
        }
    }

    internal bool Excited
    {
        set
        {
            _excited = value;
            InvalidateVisual();
        }
    }

    protected override void OnRender(DrawingContext drawing)
    {
        drawing.PushTransform(new ScaleTransform(ActualWidth / 41.0, ActualHeight / 39.0));
        drawing.DrawRoundedRectangle(Background, null, new Rect(0, 0, 41, 39), 10, 10);
        drawing.DrawEllipse(Dark, null, new Point(20, 31), 13, 2);
        drawing.DrawGeometry(Fur, null, Geometry.Parse("M8,24 L8,11 Q8,8 10,9 L16,14 L24,14 L31,9 Q33,8 33,11 L33,24 Q33,30 26,30 L15,30 Q8,30 8,24 Z"));
        drawing.DrawGeometry(InnerEar, null, Geometry.Parse("M10,12 L14,16 L10,17 Z M31,12 L27,16 L31,17 Z"));
        drawing.DrawEllipse(InnerEar, null, new Point(12.5, 24.5), 2, 1);
        drawing.DrawEllipse(InnerEar, null, new Point(28.5, 24.5), 2, 1);
        if (_excited)
        {
            var eye = new Pen(Dark, 1.4) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
            drawing.DrawGeometry(null, eye, Geometry.Parse("M13,22 Q15,19 17,22 M24,22 Q26,19 28,22"));
            drawing.DrawGeometry(Light, null, Geometry.Parse("M35,4 L36,7 L39,8 L36,9 L35,12 L34,9 L31,8 L34,7 Z"));
        }
        else
        {
            drawing.DrawEllipse(Dark, null, new Point(15, 21), 1.5, 2);
            drawing.DrawEllipse(Dark, null, new Point(26, 21), 1.5, 2);
        }
        if (_showHeart)
            drawing.DrawGeometry(InnerEar, null, Heart);
        drawing.DrawGeometry(null, new Pen(Dark, 1.15), Geometry.Parse("M18,24 Q19,26 20.5,24 Q22,26 23,24"));
        drawing.Pop();
    }
}
