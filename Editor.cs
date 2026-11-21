using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Prism;

public partial class MainWindow
{
    private static readonly string[] ShapeNames = { "Крест", "Т-форма", "Точка", "Кольцо", "Диагональ", "Шеврон", "Уголки", "Котик" };
    private static readonly string[] Colors = { "#FAFCFF", "#B6FFDB", "#6DE3FF", "#A7B8FF", "#FF92AA", "#FFE493", "#ACFF61" };

    private static readonly string[] OutlineColors = { "#000000", "#333738", "#FFFFFF", "#D9C59F", "#3E6980", "#AC5568" };

    private sealed record SliderBinding(Slider Input, TextBlock Label, TextBlock Value, Func<Reticle, int> Read,
        Func<Reticle, bool> Enabled);
    private readonly List<SliderBinding> _sliders = new();
    private bool _synchronizing;
    private int _zoom = 4;
    private (Button Button, FrameworkElement Panel)[] _tuningGroups = Array.Empty<(Button, FrameworkElement)>();
    private Button? _activeTuning;

    private void ConfigureEditor()
    {
        _synchronizing = true;
        SceneBox.ItemsSource = new[] { "Контраст", "Сетка", "Светлый", "Тёмный", "Свет и тень" };
        SceneBox.SelectedIndex = (int)PreviewScene.Grid;
        _tuningGroups = new[]
        {
            (ShapeTool, (FrameworkElement)ShapeDetails),
            (SizeTool, SizeDetails),
            (ColorTool, ColorDetails),
            (OutlineTool, OutlineDetails),
            (PositionTool, PositionDetails)
        };
        SelectTuning(SizeTool);

        foreach (var shape in Enum.GetValues<ReticleShape>())
        {
            var sample = new Reticle { Shape = shape, Length = 6, Gap = 3, DotSize = 6, Outline = false, CenterDot = false };
            var label = ShapeNames[(int)shape];
            var card = new StackPanel();
            card.Children.Add(new PresetGlyph { Width = 32, Height = 28, Reticle = sample });
            card.Children.Add(new TextBlock { Text = label, FontSize = 10, HorizontalAlignment = HorizontalAlignment.Center });
            var button = new Button { Content = card, MinHeight = 55, Padding = new Thickness(1, 0, 1, 3), Margin = new Thickness(3, 0, 3, 0) };
            button.ToolTip = label;
            AutomationProperties.SetName(button, label);
            button.Click += (_, _) => SetReticle(_settings.Reticle with { Shape = shape });
            ShapeButtons.Children.Add(button);
        }

        AddPalette(Palette, Colors, false);
        AddPalette(OutlinePalette, OutlineColors, true);

        AddSlider(GeometrySliders, "Длина", 1, 36, " px", r => r.Length, (r, v) => r with { Length = v }, r => r.Shape != ReticleShape.Dot);
        AddSlider(GeometrySliders, "Зазор", 0, 24, " px", r => r.Gap, (r, v) => r with { Gap = v }, SupportsAngles);
        AddSlider(GeometrySliders, "Толщина", 1, 10, " px", r => r.Thickness, (r, v) => r with { Thickness = v }, r => r.Shape != ReticleShape.Dot);
        AddSlider(GeometrySliders, "Размер точки", 1, 14, " px", r => r.DotSize, (r, v) => r with { DotSize = v }, r => r.CenterDot || r.Shape == ReticleShape.Dot);
        AddSlider(OutlineSliders, "Обводка", 1, 4, " px", r => r.OutlineWidth, (r, v) => r with { OutlineWidth = v }, r => r.Outline);
        AddSlider(ColorControls, "Непрозрачность", 10, 100, "%", r => r.Opacity, (r, v) => r with { Opacity = v });
        AddSlider(OffsetSliders, "По X", -100, 100, " px", r => r.OffsetX, (r, v) => r with { OffsetX = v });
        AddSlider(OffsetSliders, "По Y", -100, 100, " px", r => r.OffsetY, (r, v) => r with { OffsetY = v });

        AddSlider(AngleSliders, "Поворот", -180, 180, "°", r => r.Rotation, (r, v) => r with { Rotation = v }, SupportsAngles);
        AddSlider(VerticalSliders, "Длина по вертикали", 1, 36, " px", r => r.VerticalLength ?? r.Length,
            (r, v) => r with { VerticalLength = v }, r => SupportsArms(r) && r.VerticalLength.HasValue);
        ConfigureAdvancedReticle();

        ConfigureProfiles();
        RefreshEditor();
    }

    private void AddPalette(Panel panel, string[] palette, bool outline)
    {
        foreach (var color in palette)
        {
            var swatch = new System.Windows.Shapes.Ellipse { Width = 16, Height = 16, Fill = ReticleDrawing.Brush(color) };
            var button = new Button { Content = swatch, MinHeight = 33, Padding = new Thickness(0), Margin = new Thickness(2, 0, 2, 0), ToolTip = color };
            AutomationProperties.SetName(button, outline ? $"Контур {color}" : $"Цвет {color}");
            button.Click += (_, _) => SetReticle(outline ? _settings.Reticle with { OutlineColor = color } : _settings.Reticle with { Color = color });
            panel.Children.Add(button);
        }
    }

    private static bool SupportsArms(Reticle r) =>
        r.Shape is ReticleShape.Cross or ReticleShape.TShape or ReticleShape.Diagonal;

    private static bool SupportsAngles(Reticle r) =>
        r.Shape is not (ReticleShape.Dot or ReticleShape.Ring);

    private void SelectTuning(Button? button)
    {
        _activeTuning = button;
        foreach (var (item, panel) in _tuningGroups)
        {
            var expanded = item == button;
            item.Tag = expanded ? "selected" : null;
            AutomationProperties.SetItemStatus(item, expanded ? "Развёрнуто" : "Свёрнуто");
            panel.Visibility = expanded ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private void TuningClick(object sender, RoutedEventArgs e)
    {
        var selected = (Button)sender;
        SelectTuning(selected == _activeTuning ? null : selected);
        if (_activeTuning == selected && Keyboard.FocusedElement == selected)
            _tuningGroups.First(group => group.Button == selected).Panel.MoveFocus(new TraversalRequest(FocusNavigationDirection.First));
    }

    private void WindowKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape || _activeTuning is not { } open || !EditorPanel.IsVisible)
            return;

        SelectTuning(null);
        open.Focus();
        e.Handled = true;
    }

    private void AddSlider(Panel parent, string title, int minimum, int maximum, string units,
        Func<Reticle, int> read, Func<Reticle, int, Reticle> write, Func<Reticle, bool>? enabled = null)
    {
        var row = new StackPanel { Margin = new Thickness(7, 4, 7, 0) };
        var header = new DockPanel();
        var label = new TextBlock { Text = title, FontSize = 12 };
        var value = new TextBlock { FontFamily = new FontFamily("Cascadia Mono, Consolas"), FontSize = 11,
            Foreground = (Brush)FindResource("MutedBrush") };
        DockPanel.SetDock(value, Dock.Right);
        header.Children.Add(value);
        header.Children.Add(label);
        row.Children.Add(header);

        var slider = new Slider { Minimum = minimum, Maximum = maximum, SmallChange = 1, LargeChange = 5 };
        AutomationProperties.SetName(slider, title);
        slider.ValueChanged += (_, _) =>
        {
            value.Text = $"{(int)slider.Value}{units}";
            if (!_synchronizing)
                SetReticle(write(_settings.Reticle, (int)slider.Value));
        };
        value.Text = $"{(int)slider.Value}{units}";
        row.Children.Add(slider);
        parent.Children.Add(row);
        _sliders.Add(new SliderBinding(slider, label, value, read, enabled ?? (_ => true)));
    }

    private void SetReticle(Reticle reticle)
    {
        if (reticle == _settings.Reticle)
        {
            RefreshEditor();
            return;
        }

        RecordReticleChange(_settings.Reticle);
        _settings.Reticle = reticle;
        RefreshEditor();
        _overlay.UpdateReticle();
        ScheduleSave();
    }

    private void RefreshEditor()
    {
        var reticle = _settings.Reticle;
        _synchronizing = true;
        for (var i = 0; i < ShapeButtons.Children.Count; i++)
            ((Button)ShapeButtons.Children[i]).Tag = i == (int)reticle.Shape ? "selected" : null;

        for (var i = 0; i < Palette.Children.Count; i++)
            ((Button)Palette.Children[i]).Tag = Colors[i] == reticle.Color ? "selected" : null;

        RefreshProfileSelection(reticle);
        ShapeValue.Text = ShapeNames[(int)reticle.Shape];
        SizeValue.Text = reticle.Shape switch
        {
            ReticleShape.Dot => $"Точка {reticle.DotSize} px",
            ReticleShape.Ring => $"Диаметр {reticle.Length} px",
            _ => $"Длина {reticle.Length} px  ·  зазор {reticle.Gap} px"
        };
        ColorValue.Text = reticle.Color;
        ColorValueSwatch.Fill = ReticleDrawing.Brush(reticle.Color);
        OutlineValue.Text = reticle.Outline ? $"Включён  ·  {reticle.OutlineWidth} px" : "Выключен";
        PositionValue.Text = reticle.OffsetX == 0 && reticle.OffsetY == 0
            ? "Центр экрана"
            : $"X {reticle.OffsetX:+0;-0;0}  /  Y {reticle.OffsetY:+0;-0;0}";

        foreach (var slider in _sliders)
        {
            slider.Input.Value = slider.Read(reticle);
            slider.Input.IsEnabled = slider.Enabled(reticle);
            slider.Label.Opacity = slider.Value.Opacity = slider.Input.IsEnabled ? 1 : 0.55;
        }

        var ring = reticle.Shape == ReticleShape.Ring;
        _sliders[0].Label.Text = ring ? "Диаметр" : "Длина";
        AutomationProperties.SetName(_sliders[0].Input, _sliders[0].Label.Text);
        DotToggle.IsEnabled = reticle.Shape != ReticleShape.Dot;
        DotToggle.Opacity = DotToggle.IsEnabled ? 1 : 0.55;
        DotToggle.IsChecked = reticle.CenterDot || reticle.Shape == ReticleShape.Dot;
        OutlineToggle.IsChecked = reticle.Outline;
        SquareToggle.IsChecked = reticle.SquareDot;
        SquareToggle.IsEnabled = reticle.CenterDot || reticle.Shape == ReticleShape.Dot;
        RoundToggle.IsChecked = reticle.RoundedEnds;
        RoundToggle.IsEnabled = SupportsAngles(reticle);
        VerticalToggle.IsEnabled = SupportsArms(reticle);
        VerticalToggle.IsChecked = reticle.VerticalLength.HasValue;
        VerticalSliders.Visibility = SupportsArms(reticle) && reticle.VerticalLength.HasValue ? Visibility.Visible : Visibility.Collapsed;
        OutlinePalette.IsEnabled = reticle.Outline;
        for (int i = 0; i < OutlineColors.Length; i++)
            ((Button)OutlinePalette.Children[i]).Tag = OutlineColors[i] == reticle.OutlineColor ? "selected" : null;
        HexInput.Text = reticle.Color;
        HexInput.Tag = null;

        RefreshAdvancedReticle(reticle);
        _synchronizing = false;
        RefreshPreview();
    }

    private void RefreshPreview()
    {
        var reticle = _settings.Reticle;
        ZoomLabel.Text = $"{_zoom}×";
        ZoomOut.IsEnabled = _zoom > 1;
        ZoomIn.IsEnabled = _zoom < 6;
        Preview.Refresh(reticle, (PreviewScene)Math.Max(0, SceneBox.SelectedIndex), _zoom);
    }

    private void PreviewChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_synchronizing && IsInitialized)
            RefreshPreview();
    }

    private void ZoomOutClick(object sender, RoutedEventArgs e)
    {
        _zoom = Math.Max(1, _zoom - 1);
        RefreshPreview();
    }

    private void ZoomInClick(object sender, RoutedEventArgs e)
    {
        _zoom = Math.Min(6, _zoom + 1);
        RefreshPreview();
    }

    private void ResetClick(object sender, RoutedEventArgs e)
    {
        _activePresetName = BuiltIns[0].Name;
        SetReticle(new Reticle());
        Notice.Text = "Параметры прицела сброшены.";
    }
    private void DetailChanged(object sender, RoutedEventArgs e)
    {
        if (_synchronizing)
            return;

        var current = _settings.Reticle;
        if (sender == VerticalToggle)
            SetReticle(current with { VerticalLength = VerticalToggle.IsChecked == true ? current.Length : null });
        else if (sender == RoundToggle)
            SetReticle(current with { RoundedEnds = RoundToggle.IsChecked == true });
        else if (sender == SquareToggle)
            SetReticle(current with { SquareDot = SquareToggle.IsChecked == true });
    }

    private void DotChanged(object sender, RoutedEventArgs e)
    {
        if (!_synchronizing)
            SetReticle(_settings.Reticle with { CenterDot = DotToggle.IsChecked == true });
    }

    private void OutlineChanged(object sender, RoutedEventArgs e)
    {
        if (!_synchronizing)
            SetReticle(_settings.Reticle with { Outline = OutlineToggle.IsChecked == true });
    }

    private void HexKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            ApplyHex();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            HexInput.Text = _settings.Reticle.Color;
            HexInput.Tag = null;
            Notice.Text = "";
            e.Handled = true;
        }
    }

    private void HexLostFocus(object sender, KeyboardFocusChangedEventArgs e) => ApplyHex();

    private void ApplyHex()
    {
        var color = Reticle.NormalizeColor(HexInput.Text);
        if (color is null)
        {
            HexInput.Tag = "invalid";
            Notice.Text = "HEX-цвет должен содержать шесть цифр или букв A–F.";
            return;
        }

        HexInput.Tag = null;
        HexInput.Text = color;
        Notice.Text = "";
        SetReticle(_settings.Reticle with { Color = color });
    }

}
