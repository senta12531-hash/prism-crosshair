using System;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Prism;

public partial class MainWindow
{
    private static readonly (ReticleArms Flag, string Glyph, string Direction)[] ArmChoices =
    {
        (ReticleArms.Top, "↑", "верхний"),
        (ReticleArms.Right, "→", "правый"),
        (ReticleArms.Bottom, "↓", "нижний"),
        (ReticleArms.Left, "←", "левый")
    };
    private static readonly string?[] DotColors = { null, "#FAFCFF", "#6DE3FF", "#D9C59F", "#FF92AA" };
    private readonly StackPanel _armControls = new();
    private readonly Button[] _armButtons = new Button[ArmChoices.Length];
    private readonly StackPanel _verticalGapControls = new();
    private readonly CheckBox _verticalGapToggle = new() { Content = "Отдельный зазор по вертикали" };
    private readonly StackPanel _verticalGapSlider = new();
    private readonly StackPanel _dotControls = new();
    private readonly Button[] _dotColorButtons = new Button[DotColors.Length];

    private void ConfigureAdvancedReticle()
    {
        _armControls.Margin = new Thickness(7, 6, 7, 0);
        _armControls.Children.Add(InspectorLabel("Видимые лучи"));
        var arms = new UniformGrid { Columns = ArmChoices.Length, Margin = new Thickness(-2, 0, -2, 0) };
        for (var i = 0; i < ArmChoices.Length; i++)
        {
            var (flag, glyph, direction) = ArmChoices[i];
            var button = InspectorChoice(glyph, $"Луч · {direction}");
            button.Click += (_, _) =>
            {
                var current = _settings.Reticle;
                SetReticle(current with { VisibleArms = current.VisibleArms ^ flag });
            };
            _armButtons[i] = button;
            arms.Children.Add(button);
        }
        _armControls.Children.Add(arms);
        AngleSliders.Children.Add(_armControls);

        _verticalGapControls.Margin = new Thickness(7, 4, 7, 0);
        _verticalGapSlider.Margin = new Thickness(-7, 0, -7, 0);
        _verticalGapToggle.Checked += VerticalGapChanged;
        _verticalGapToggle.Unchecked += VerticalGapChanged;
        _verticalGapControls.Children.Add(_verticalGapToggle);
        _verticalGapControls.Children.Add(_verticalGapSlider);
        GeometrySliders.Children.Add(_verticalGapControls);
        AddSlider(_verticalGapSlider, "Зазор по вертикали", 0, 24, " px",
            r => r.VerticalGap ?? r.Gap, (r, v) => r with { VerticalGap = v });

        AddSlider(OutlineSliders, "Непрозрачность контура", 0, 100, "%",
            r => r.OutlineOpacity, (r, v) => r with { OutlineOpacity = v }, r => r.Outline);

        _dotControls.Margin = new Thickness(7, 12, 7, 0);
        _dotControls.Children.Add(InspectorLabel("Цвет точки"));
        var palette = new UniformGrid { Columns = DotColors.Length, Margin = new Thickness(-2, 0, -2, 0) };
        for (var i = 0; i < DotColors.Length; i++)
        {
            var color = DotColors[i];
            object content = color is null ? "Основной" : new Ellipse
            {
                Width = 15,
                Height = 15,
                Fill = ReticleDrawing.Brush(color)
            };
            var button = InspectorChoice(content, color is null ? "Основной цвет точки" : $"Цвет точки {color}");
            button.FontSize = 11;
            button.Click += (_, _) => SetReticle(_settings.Reticle with { DotColor = color });
            _dotColorButtons[i] = button;
            palette.Children.Add(button);
        }
        _dotControls.Children.Add(palette);
        var dotOpacity = new StackPanel { Margin = new Thickness(-7, 4, -7, 0) };
        _dotControls.Children.Add(dotOpacity);
        ColorControls.Children.Add(_dotControls);
        AddSlider(dotOpacity, "Непрозрачность точки", 10, 100, "%",
            r => r.DotOpacity, (r, v) => r with { DotOpacity = v });
    }

    private void VerticalGapChanged(object sender, RoutedEventArgs e)
    {
        if (_synchronizing)
            return;

        var current = _settings.Reticle;
        int? gap = _verticalGapToggle.IsChecked == true ? current.Gap : null;
        SetReticle(current with { VerticalGap = gap });
    }

    private TextBlock InspectorLabel(string text) =>
        new() { Text = text, Margin = new Thickness(0, 0, 0, 6), Style = (Style)FindResource("Caption") };

    private static Button InspectorChoice(object content, string label)
    {
        var button = new Button
        {
            Content = content,
            MinHeight = 33,
            Margin = new Thickness(2, 0, 2, 0),
            Padding = new Thickness(0),
            ToolTip = label
        };
        AutomationProperties.SetName(button, label);
        return button;
    }

    private void RefreshAdvancedReticle(Reticle reticle)
    {
        var supportsIndividualArms = SupportsArms(reticle);
        var armVisibility = supportsIndividualArms ? Visibility.Visible : Visibility.Collapsed;
        _armControls.Visibility = _verticalGapControls.Visibility = armVisibility;
        _verticalGapToggle.IsChecked = reticle.VerticalGap.HasValue;
        _verticalGapSlider.Visibility = reticle.VerticalGap.HasValue ? Visibility.Visible : Visibility.Collapsed;
        _dotControls.Visibility = reticle.CenterDot || reticle.Shape == ReticleShape.Dot
            ? Visibility.Visible : Visibility.Collapsed;

        for (var i = 0; i < ArmChoices.Length; i++)
        {
            var flag = ArmChoices[i].Flag;
            var button = _armButtons[i];
            var available = reticle.Shape != ReticleShape.TShape || flag != ReticleArms.Top;
            var visible = available && reticle.VisibleArms.HasFlag(flag);
            button.IsEnabled = available;
            button.Tag = visible ? "selected" : null;
            AutomationProperties.SetItemStatus(button, visible ? "Показан" : "Скрыт");
        }

        for (var i = 0; i < DotColors.Length; i++)
        {
            var button = _dotColorButtons[i];
            var selected = DotColors[i] == reticle.DotColor;
            button.Tag = selected ? "selected" : null;
            AutomationProperties.SetItemStatus(button, selected ? "Выбран" : "");
        }
    }
}
