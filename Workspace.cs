using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Prism;

public partial class MainWindow
{
    private static readonly string[] Tips =
    {
        "Нажми на значение масштаба, чтобы увидеть прицел в реальном размере.",
        "Открой «До / Сейчас» после правки и сравни результат с предыдущим состоянием.",
        "Фон «Свет и тень» показывает, насколько контур читается на светлых и тёмных участках.",
        "Прицел можно настроить на конкретный монитор во вкладке «Настройки».",
        "Сохрани понравившийся прицел во вкладке «Профили», чтобы вернуться к нему позже."
    };

    private readonly List<Reticle> _undoHistory = new();
    private readonly List<Reticle> _redoHistory = new();
    private Thumb? _historyDrag;
    private bool _replayingHistory;
    private bool _workspaceInitialized;
    private bool _guidesVisible = true;
    private Reticle? _comparison;
    private int _tipIndex;

    private void WorkspaceLoaded(object sender, RoutedEventArgs e)
    {
        if (_workspaceInitialized)
            return;

        _workspaceInitialized = true;
        TipText.Text = Tips[0];
        RefreshWorkspaceButtons();
    }

    private void RecordReticleChange(Reticle before)
    {
        if (_replayingHistory)
            return;

        var drag = Mouse.Captured as Thumb;
        if (drag is not null && drag == _historyDrag)
            return;

        _historyDrag = drag;
        if (_undoHistory.Count == 64)
            _undoHistory.RemoveAt(0);

        _undoHistory.Add(before);
        _redoHistory.Clear();
        RefreshWorkspaceButtons();
    }

    private void HistoryCaptureLost(object sender, MouseEventArgs e)
    {
        if (e.OriginalSource == _historyDrag)
            _historyDrag = null;
    }

    private void UndoClick(object sender, RoutedEventArgs e) => RestoreHistory(_undoHistory, _redoHistory);
    private void RedoClick(object sender, RoutedEventArgs e) => RestoreHistory(_redoHistory, _undoHistory);

    private void RestoreHistory(List<Reticle> source, List<Reticle> destination)
    {
        if (source.Count == 0)
            return;

        var restored = source[^1];
        source.RemoveAt(source.Count - 1);
        destination.Add(_settings.Reticle);

        _replayingHistory = true;
        try
        {
            SetReticle(restored);
        }
        finally
        {
            _replayingHistory = false;
        }

        RefreshWorkspaceButtons();
    }

    private void RefreshWorkspaceButtons()
    {
        UndoButton.IsEnabled = _undoHistory.Count > 0;
        RedoButton.IsEnabled = _redoHistory.Count > 0;
        CompareButton.IsEnabled = _comparison is not null || _undoHistory.Count > 0;
        CompareButton.Tag = _comparison is not null ? "selected" : null;
        GuideButton.Tag = _guidesVisible ? "selected" : null;
    }

    private void WorkspaceKeyDown(object sender, KeyEventArgs e)
    {
        if (!EditorPanel.IsVisible || e.OriginalSource is TextBoxBase || Keyboard.Modifiers != ModifierKeys.Control)
            return;

        if (e.Key == Key.Z)
            RestoreHistory(_undoHistory, _redoHistory);
        else if (e.Key == Key.Y)
            RestoreHistory(_redoHistory, _undoHistory);
        else
            return;

        e.Handled = true;
    }

    private void GuidesClick(object sender, RoutedEventArgs e)
    {
        _guidesVisible = !_guidesVisible;
        UpdateDisplayOptions();
    }

    private void CompareClick(object sender, RoutedEventArgs e)
    {
        _comparison = _comparison is null && _undoHistory.Count > 0 ? _undoHistory[^1] : null;
        UpdateDisplayOptions();
    }

    private void UpdateDisplayOptions()
    {
        Preview.SetDisplayOptions(_guidesVisible, _comparison);
        RefreshWorkspaceButtons();
    }

    private void ActualSizeClick(object sender, RoutedEventArgs e)
    {
        _zoom = 1;
        RefreshPreview();
        Notice.Text = "Масштаб 1:1. Толщина и размер показаны в пикселях экрана.";
    }

    private void NextTipClick(object sender, RoutedEventArgs e)
    {
        _tipIndex = (_tipIndex + 1) % Tips.Length;
        TipText.Text = Tips[_tipIndex];
        Pixel.ShowHeart = false;
        Pixel.Excited = _tipIndex % 2 != 0;
        TryCatButton.Visibility = Visibility.Collapsed;
        AnimatePixel();
    }

    private void PetGuideClick(object sender, RoutedEventArgs e)
    {
        TipText.Text = "Мур-р! Хочешь примерить мои ушки?";
        Pixel.Excited = true;
        Pixel.ShowHeart = true;
        TryCatButton.Visibility = Visibility.Visible;
        AnimatePixel();
    }

    private void TryCatClick(object sender, RoutedEventArgs e)
    {
        ApplyPreset(CatPreset, navigateToEditor: false);
        TipText.Text = "Готово! «Котик» на холсте. Можешь сохранить свой вариант в профилях.";
        TryCatButton.Visibility = Visibility.Collapsed;
        AnimatePixel();
    }

    private void AnimatePixel()
    {

        if (!AllowsMotion)
            return;

        var hop = new DoubleAnimationUsingKeyFrames { Duration = TimeSpan.FromMilliseconds(255) };
        hop.KeyFrames.Add(new EasingDoubleKeyFrame(0, KeyTime.FromPercent(0)));
        hop.KeyFrames.Add(new EasingDoubleKeyFrame(-5, KeyTime.FromPercent(0.45),
            new QuadraticEase { EasingMode = EasingMode.EaseOut }));
        hop.KeyFrames.Add(new EasingDoubleKeyFrame(0, KeyTime.FromPercent(1),
            new QuadraticEase { EasingMode = EasingMode.EaseIn }));
        var motion = new TranslateTransform();
        Pixel.RenderTransform = motion;
        motion.BeginAnimation(TranslateTransform.YProperty, hop);
        TipText.BeginAnimation(OpacityProperty, new DoubleAnimation(0.55, 1.0, TimeSpan.FromMilliseconds(170)));
    }

    private void AnimateWorkspacePage(FrameworkElement page)
    {
        page.BeginAnimation(OpacityProperty, AllowsMotion
            ? new DoubleAnimation(0.65, 1.0, TimeSpan.FromMilliseconds(170))
                { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut } }
            : null);
    }
}
