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
    private static readonly string[] ShapeNames = { "Крест", "Т-форма", "Точка", "Кольцо", "Диагональ" };
    private static readonly string[] Colors = { "#B6FFDB", "#F8F9FF", "#6DE3FF", "#B5A3FF", "#FF92AA", "#FFE493", "#ACFF61" };

    private static readonly Preset[] BuiltIns =
    {
        new("Призма", "Классический", new Reticle()),
        new("Искра", "Точка", new Reticle { Shape = ReticleShape.Dot, Color = "#FF92AA", DotSize = 5 }),
        new("Орбита", "Кольцо", new Reticle { Shape = ReticleShape.Ring, Color = "#B5A3FF", Length = 19, CenterDot = false }),
        new("Вектор", "Т-образный", new Reticle { Shape = ReticleShape.TShape, Color = "#6DE3FF", Length = 13, Gap = 3, CenterDot = false })
    };

    private sealed record SliderBinding(Slider Input, TextBlock Label, TextBlock Value, Func<Reticle, int> Read,
        Func<Reticle, bool> Enabled);
    private readonly List<SliderBinding> _sliders = new();
    private readonly List<(Button Button, Preset Preset)> _presetButtons = new();
    private bool _synchronizing;
    private int _zoom = 4;

    private void ConfigureEditor()
    {
        _synchronizing = true;
        SceneBox.ItemsSource = new[] { "Сетка", "Светлый", "Тёмный" };
        SceneBox.SelectedIndex = 0;

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

        foreach (var color in Colors)
        {
            var swatch = new System.Windows.Shapes.Ellipse { Width = 16, Height = 16, Fill = ReticleDrawing.Brush(color) };
            var button = new Button { Content = swatch, MinHeight = 33, Padding = new Thickness(0), Margin = new Thickness(2, 0, 2, 0), ToolTip = color };
            AutomationProperties.SetName(button, $"Цвет {color}");
            button.Click += (_, _) => SetReticle(_settings.Reticle with { Color = color });
            Palette.Children.Add(button);
        }

        AddSlider(GeometrySliders, "Длина", 1, 36, " px", r => r.Length, (r, v) => r with { Length = v }, r => r.Shape != ReticleShape.Dot);
        AddSlider(GeometrySliders, "Зазор", 0, 24, " px", r => r.Gap, (r, v) => r with { Gap = v }, SupportsArms);
        AddSlider(GeometrySliders, "Толщина", 1, 10, " px", r => r.Thickness, (r, v) => r with { Thickness = v }, r => r.Shape != ReticleShape.Dot);
        AddSlider(GeometrySliders, "Размер точки", 1, 14, " px", r => r.DotSize, (r, v) => r with { DotSize = v }, r => r.CenterDot || r.Shape == ReticleShape.Dot);
        AddSlider(GeometrySliders, "Обводка", 1, 4, " px", r => r.OutlineWidth, (r, v) => r with { OutlineWidth = v }, r => r.Outline);
        AddSlider(ColorControls, "Непрозрачность", 10, 100, "%", r => r.Opacity, (r, v) => r with { Opacity = v });
        AddSlider(OffsetSliders, "По X", -100, 100, " px", r => r.OffsetX, (r, v) => r with { OffsetX = v });
        AddSlider(OffsetSliders, "По Y", -100, 100, " px", r => r.OffsetY, (r, v) => r with { OffsetY = v });

        BuildPresetCards();
        RefreshEditor();
    }

    private static bool SupportsArms(Reticle r) =>
        r.Shape is ReticleShape.Cross or ReticleShape.TShape or ReticleShape.Diagonal;

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
            return;

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

        foreach (var (button, preset) in _presetButtons)
            button.Tag = reticle == preset.Reticle ? "selected" : null;

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
        HexInput.Text = reticle.Color;
        HexInput.Tag = null;

        _synchronizing = false;
        RefreshPreview();
    }

    private void RefreshPreview()
    {
        var reticle = _settings.Reticle;
        PreviewShapeLabel.Text = ShapeNames[(int)reticle.Shape];
        Coordinates.Text = $"X {reticle.OffsetX:+0;-0;0}  /  Y {reticle.OffsetY:+0;-0;0}";
        ZoomLabel.Text = $"{_zoom}×";
        ZoomOut.IsEnabled = _zoom > 1;
        ZoomIn.IsEnabled = _zoom < 6;
        Preview.Refresh(reticle, SceneBox.SelectedIndex, _zoom);
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
        SetReticle(new Reticle());
        Notice.Text = "Параметры прицела сброшены.";
    }
    private void DotClick(object sender, RoutedEventArgs e) => SetReticle(_settings.Reticle with { CenterDot = DotToggle.IsChecked == true });
    private void OutlineClick(object sender, RoutedEventArgs e) => SetReticle(_settings.Reticle with { Outline = OutlineToggle.IsChecked == true });

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

    private void BuildPresetCards()
    {
        PresetCards.Children.Clear();
        _presetButtons.Clear();

        foreach (var preset in BuiltIns)
            AddPresetCard(preset, custom: false);

        foreach (var preset in _settings.Presets)
            AddPresetCard(preset, custom: true);
    }

    private void AddPresetCard(Preset preset, bool custom)
    {
        var layout = new Grid { Width = 136, Height = 66 };
        layout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(42) });
        layout.ColumnDefinitions.Add(new ColumnDefinition());
        var icon = new Border
        {
            Width = 36, Height = 36, HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center, CornerRadius = new CornerRadius(7),
            Background = ReticleDrawing.Brush("#1B1C28"), Child = new PresetGlyph { Reticle = preset.Reticle }
        };
        var copy = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(3, 0, 0, 0) };
        copy.Children.Add(new TextBlock { Text = preset.Name, FontWeight = FontWeights.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis });
        copy.Children.Add(new TextBlock { Text = preset.Description, FontSize = 10, TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(0, 3, 0, 0),
            Foreground = (Brush)FindResource("MutedBrush") });
        Grid.SetColumn(copy, 1);
        layout.Children.Add(icon);
        layout.Children.Add(copy);
        var button = new Button
        {
            Content = layout, Padding = new Thickness(8, 0, 8, 0), VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 0, 7, 0), ToolTip = custom ? $"{preset.Name} · ПКМ для удаления" : preset.Name
        };
        AutomationProperties.SetName(button, $"Загрузить пресет {preset.Name}");
        button.Click += (_, _) =>
        {
            SetReticle(preset.Reticle);
            Notice.Text = $"Пресет «{preset.Name}» загружен.";
        };
        if (custom)
        {
            var menu = new ContextMenu();
            var delete = new MenuItem { Header = $"Удалить «{preset.Name}»" };
            delete.Click += (_, _) =>
            {
                _settings.Presets.Remove(preset);
                BuildPresetCards();
                RefreshEditor();
                ScheduleSave();
                Notice.Text = $"Пресет «{preset.Name}» удалён.";
            };
            menu.Items.Add(delete);
            button.ContextMenu = menu;
        }
        PresetCards.Children.Add(button);
        _presetButtons.Add((button, preset));
    }

    private void SavePresetClick(object sender, RoutedEventArgs e)
    {
        var dialog = new Window
        {
            Owner = this, Title = "Новый пресет", Width = 380, SizeToContent = SizeToContent.Height,
            WindowStartupLocation = WindowStartupLocation.CenterOwner, ShowInTaskbar = false, ResizeMode = ResizeMode.NoResize,
            Background = (Brush)FindResource("WindowBrush"), FontFamily = FontFamily, FontSize = FontSize, Icon = Icon,
            Foreground = (Brush)FindResource("TextBrush")
        };
        var content = new StackPanel { Margin = new Thickness(20) };
        var label = new TextBlock { Text = "Название пресета", Margin = new Thickness(0, 0, 0, 7), FontWeight = FontWeights.SemiBold,
            Foreground = dialog.Foreground };
        var name = new TextBox { MaxLength = 30, Text = "Мой прицел" };
        AutomationProperties.SetLabeledBy(name, label);
        var error = new TextBlock { Foreground = (Brush)FindResource("DangerBrush"), Margin = new Thickness(0, 7, 0, 0) };

        var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
        var cancel = new Button { Content = "Отмена", IsCancel = true, Margin = new Thickness(0, 0, 6, 0) };
        var save = new Button { Content = "Сохранить", Tag = "primary", IsDefault = true };
        actions.Children.Add(cancel);
        actions.Children.Add(save);
        content.Children.Add(label);
        content.Children.Add(name);
        content.Children.Add(error);
        content.Children.Add(actions);
        dialog.Content = content;
        dialog.Loaded += (_, _) => { name.Focus(); name.SelectAll(); };

        save.Click += (_, _) =>
        {
            var candidate = name.Text.Trim();
            if (string.IsNullOrWhiteSpace(candidate))
            {
                error.Text = "Введите название.";
                return;
            }
            if (BuiltIns.Concat(_settings.Presets).Any(preset => string.Equals(preset.Name, candidate, StringComparison.OrdinalIgnoreCase)))
            {
                error.Text = "Пресет с таким названием уже существует.";
                return;
            }
            _settings.Presets.Insert(0, new Preset(candidate, "Свой прицел", _settings.Reticle));
            dialog.DialogResult = true;
        };

        if (dialog.ShowDialog() == true)
        {
            BuildPresetCards();
            ((FrameworkElement)PresetCards.Children[BuiltIns.Length]).BringIntoView();
            RefreshEditor();
            ScheduleSave();
            Notice.Text = $"Пресет «{name.Text.Trim()}» сохранён.";
        }
    }
}
