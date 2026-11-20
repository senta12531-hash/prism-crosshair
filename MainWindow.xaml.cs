using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Threading;
using Forms = System.Windows.Forms;

namespace Prism;

public partial class MainWindow : Window
{
    internal const string WindowTitle = "Prism Crosshair";
    private readonly Settings _settings = Settings.Load();
    private readonly OverlayWindow _overlay;
    private readonly DispatcherTimer _saveDebounce = new() { Interval = TimeSpan.FromMilliseconds(280) };
    private readonly DispatcherTimer _updateTimer = new() { Interval = TimeSpan.FromHours(4) };
    private readonly CancellationTokenSource _lifetime = new();
    private readonly Forms.NotifyIcon _tray;
    private readonly Forms.ToolStripMenuItem _trayToggle;
    private readonly System.Drawing.Icon _trayIcon;
    private HwndSource? _source;
    private Updates.Update? _update;
    private bool _checking;
    private bool _loadingMonitors;
    private int _hotkeyId;

    private sealed record Choice<T>(T Value, string Label)
    {
        public override string ToString() => Label;
    }

    private static readonly Choice<int>[] Modifiers =
    {
        new(6, "Ctrl + Shift"), new(3, "Ctrl + Alt"), new(5, "Alt + Shift")
    };

    public MainWindow()
    {
        InitializeComponent();
        Width = Math.Max(MinWidth, Math.Min(Width, SystemParameters.WorkArea.Width - 26));
        Height = Math.Max(MinHeight, Math.Min(Height, SystemParameters.WorkArea.Height - 26));

        _overlay = new OverlayWindow(_settings);
        ConfigureEditor();

        ModifierBox.ItemsSource = Modifiers;
        ModifierBox.SelectedItem = Modifiers.First(item => item.Value == _settings.HotkeyModifiers);
        KeyBox.ItemsSource = Enumerable.Range(1, 12).Select(i => new Choice<int>(0x6F + i, $"F{i}")).ToArray();
        KeyBox.SelectedIndex = _settings.HotkeyKey - 0x70;
        TrayToggle.IsChecked = _settings.MinimizeToTray;
        UpdatesToggle.IsChecked = _settings.CheckUpdates;
        SettingsVersion.Text = FooterVersion.Text = Updates.VersionLabel;
        UpdateFeedback.Text = "Новые версии загружаются из GitHub Releases.";
        PopulateMonitors();

        using var iconResource = Application.GetResourceStream(new Uri("Assets/Prism.ico", UriKind.Relative))!.Stream;
        _trayIcon = new System.Drawing.Icon(iconResource);
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Открыть Prism", null, (_, _) => ShowEditor());
        _trayToggle = (Forms.ToolStripMenuItem)menu.Items.Add("", null, (_, _) => ToggleOverlay());
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Выход", null, (_, _) => Close());
        _tray = new Forms.NotifyIcon { Icon = _trayIcon, Text = WindowTitle, ContextMenuStrip = menu, Visible = true };
        _tray.DoubleClick += (_, _) => ShowEditor();
        _tray.BalloonTipClicked += (_, _) => ShowEditor();

        _saveDebounce.Tick += (_, _) => PersistSettings();
        _updateTimer.Tick += async (_, _) => await CheckUpdatesAsync(manual: false);

        SourceInitialized += (_, _) =>
        {
            _source = (HwndSource)PresentationSource.FromVisual(this);
            _source.AddHook(WindowMessages);
            AssignHotkey(_settings.HotkeyModifiers, _settings.HotkeyKey);
        };
        Loaded += async (_, _) =>
        {
            UpdateOverlayState();
            if (_settings.CheckUpdates)
            {
                _updateTimer.Start();
                await CheckUpdatesAsync(manual: false);
            }
        };
        StateChanged += (_, _) =>
        {
            var maximized = WindowState == WindowState.Maximized;
            MaximizeButton.Content = maximized ? "\uE923" : "\uE922";
            var caption = maximized ? "Восстановить размер" : "Развернуть";
            MaximizeButton.ToolTip = caption;
            AutomationProperties.SetName(MaximizeButton, caption);
            if (WindowState == WindowState.Minimized && _settings.MinimizeToTray)
                Hide();
        };
    }

    private IntPtr WindowMessages(IntPtr hwnd, int msg, IntPtr wp, IntPtr lp, ref bool handled)
    {
        switch (msg)
        {
            case Native.HotkeyMessage when _hotkeyId != 0 && wp.ToInt32() == _hotkeyId:
                ToggleOverlay();
                handled = true;
                break;
            case Native.ShowEditorMessage:
                ShowEditor();
                handled = true;
                break;
            case Native.DisplayChangeMessage:
                Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
                {
                    PopulateMonitors();
                    _overlay.QueuePlacement();
                }));
                break;
        }
        return IntPtr.Zero;
    }

    private void EditorClick(object sender, RoutedEventArgs e) => ShowPage(EditorPanel, EditorTab);
    private void ProfilesClick(object sender, RoutedEventArgs e) => ShowPage(ProfilesPanel, ProfilesTab);
    private void SettingsClick(object sender, RoutedEventArgs e) => ShowPage(SettingsPanel, SettingsTab);

    private void ShowPage(FrameworkElement activePanel, Button activeTab)
    {
        foreach (var panel in new[] { EditorPanel, ProfilesPanel, SettingsPanel })
            panel.Visibility = panel == activePanel ? Visibility.Visible : Visibility.Collapsed;
        foreach (var tab in new[] { EditorTab, ProfilesTab, SettingsTab })
            tab.Tag = tab == activeTab ? "active" : null;
    }

    private void ShowEditor()
    {
        if (!IsVisible)
            Show();
        if (WindowState == WindowState.Minimized)
            WindowState = WindowState.Normal;
        Activate();
    }

    private void MinimizeClick(object sender, RoutedEventArgs e) => SystemCommands.MinimizeWindow(this);
    private void MaximizeClick(object sender, RoutedEventArgs e)
    {
        if (WindowState == WindowState.Maximized)
            SystemCommands.RestoreWindow(this);
        else
            SystemCommands.MaximizeWindow(this);
    }
    private void CloseClick(object sender, RoutedEventArgs e) => Close();

    private void OverlayClick(object sender, RoutedEventArgs e) => ToggleOverlay();

    private void ToggleOverlay()
    {
        _settings.OverlayEnabled = !_settings.OverlayEnabled;
        UpdateOverlayState();
        ScheduleSave();
    }

    private void UpdateOverlayState()
    {
        var enabled = _settings.OverlayEnabled;
        _overlay.SetEnabled(enabled);
        OverlayButton.Tag = enabled ? "selected" : "primary";
        OverlayButtonLabel.Text = enabled ? "Скрыть прицел" : "Включить прицел";
        AutomationProperties.SetName(OverlayButton, OverlayButtonLabel.Text);
        FooterOverlayStatus.Text = enabled ? "Оверлей включён" : "Оверлей выключен";
        OverlayIndicator.Fill = (System.Windows.Media.Brush)FindResource(enabled ? "AccentBrush" : "MutedBrush");
        _trayToggle.Text = OverlayButtonLabel.Text;
    }

    private void AssignHotkey(int modifiers, int key)
    {
        var label = $"{Modifiers.First(item => item.Value == modifiers).Label} + F{key - 0x6F}";
        if (_hotkeyId != 0 && modifiers == _settings.HotkeyModifiers && key == _settings.HotkeyKey)
        {
            HotkeyFeedback.Text = $"Активная комбинация — {label}";
            return;
        }

        var newId = _hotkeyId == 1 ? 2 : 1;
        if (_source is null || !Native.RegisterHotKey(_source.Handle, newId, (uint)modifiers | 0x4000, (uint)key))
        {
            HotkeyFeedback.Text = $"Комбинация {label} недоступна. Выберите другую.";
            if (_hotkeyId == 0)
                HeaderShortcut.Text = "Нет клавиш";
            Notice.Text = HotkeyFeedback.Text;
            return;
        }

        if (_hotkeyId != 0)
            Native.UnregisterHotKey(_source.Handle, _hotkeyId);
        _hotkeyId = newId;
        _settings.HotkeyModifiers = modifiers;
        _settings.HotkeyKey = key;
        HeaderShortcut.Text = label;
        HotkeyFeedback.Text = $"Активная комбинация — {label}";
        Notice.Text = "";
    }

    private void ApplyHotkeyClick(object sender, RoutedEventArgs e)
    {
        if (ModifierBox.SelectedItem is Choice<int> modifiers && KeyBox.SelectedItem is Choice<int> key)
        {
            AssignHotkey(modifiers.Value, key.Value);
            ScheduleSave();
        }
    }

    private void PopulateMonitors()
    {
        _loadingMonitors = true;
        var screens = Forms.Screen.AllScreens;
        var monitors = new[] { new Choice<string?>(null, "Автоматически") }.Concat(screens.Select((screen, i) =>
            new Choice<string?>(screen.DeviceName,
                $"Экран {i + 1}  ·  {screen.Bounds.Width} × {screen.Bounds.Height}{(screen.Primary ? "  ·  основной" : "")}"))).ToArray();
        MonitorBox.ItemsSource = monitors;
        MonitorBox.SelectedItem = _settings.Placement == PlacementMode.ActiveMonitor
            ? monitors[0]
            : monitors.FirstOrDefault(item => item.Value == _settings.MonitorDevice)
              ?? new Choice<string?>(_settings.MonitorDevice, "Выбранный монитор отключён");
        if (MonitorBox.SelectedItem is null)
        {
            MonitorBox.ItemsSource = monitors.Append(new Choice<string?>(_settings.MonitorDevice, "Выбранный монитор отключён"));
            MonitorBox.SelectedIndex = monitors.Length;
        }
        _loadingMonitors = false;
    }

    private void MonitorChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loadingMonitors || MonitorBox.SelectedItem is not Choice<string?> monitor)
            return;

        _settings.Placement = monitor.Value is null ? PlacementMode.ActiveMonitor : PlacementMode.FixedMonitor;
        _settings.MonitorDevice = monitor.Value;
        _overlay.RefreshTracking();
        ScheduleSave();
    }

    private void TrayPreferenceChanged(object sender, RoutedEventArgs e)
    {
        if (_source is null)
            return;

        _settings.MinimizeToTray = TrayToggle.IsChecked == true;
        ScheduleSave();
    }

    private async void UpdatePreferenceChanged(object sender, RoutedEventArgs e)
    {
        if (_source is null)
            return;

        _settings.CheckUpdates = UpdatesToggle.IsChecked == true;
        ScheduleSave();
        if (_settings.CheckUpdates)
        {
            _updateTimer.Start();
            await CheckUpdatesAsync(manual: false);
        }
        else
            _updateTimer.Stop();
    }

    private async void CheckUpdateClick(object sender, RoutedEventArgs e) => await CheckUpdatesAsync(manual: true);

    private async Task CheckUpdatesAsync(bool manual)
    {
        if (_checking)
            return;

        _checking = true;
        CheckUpdateButton.IsEnabled = false;
        UpdateFeedback.Text = "Проверяем GitHub…";
        try
        {
            var previous = _update?.Version;
            _update = await Updates.CheckAsync(_lifetime.Token);
            if (_update is { } available)
            {
                var label = $"Скачать v{available.Version}  ↗";
                UpdateAvailable.Content = label;
                ReleaseButton.Content = available.IsInstaller ? "Скачать установщик ↗" : "Скачать ZIP ↗";
                UpdateAvailable.Visibility = Visibility.Visible;
                UpdateFeedback.Text = $"Вышла версия {available.Version}.";
                if (!manual && previous != available.Version && !IsVisible)
                    _tray.ShowBalloonTip(4000, "Доступно обновление Prism", UpdateFeedback.Text, Forms.ToolTipIcon.Info);
            }
            else
            {
                UpdateAvailable.Visibility = Visibility.Collapsed;
                ReleaseButton.Content = "GitHub ↗";
                UpdateFeedback.Text = "У вас последняя версия.";
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or JsonException or
                                   KeyNotFoundException or InvalidOperationException or OperationCanceledException)
        {
            if (!_lifetime.IsCancellationRequested)
                UpdateFeedback.Text = "GitHub не ответил. Попробуйте проверить ещё раз.";
        }
        finally
        {
            _checking = false;
            CheckUpdateButton.IsEnabled = true;
        }
    }

    private void OpenReleaseClick(object sender, RoutedEventArgs e)
    {
        try
        {
            Updates.Open(_update?.Download.AbsoluteUri ?? Updates.ReleasesUrl);
        }
        catch (Win32Exception)
        {
            Notice.Text = "Не удалось открыть GitHub в браузере.";
        }
    }

    private void ScheduleSave()
    {
        _saveDebounce.Stop();
        _saveDebounce.Start();
    }

    private void PersistSettings()
    {
        _saveDebounce.Stop();
        if (!_settings.Save())
            Notice.Text = "Не удалось сохранить настройки.";
    }

    protected override void OnClosed(EventArgs e)
    {
        _updateTimer.Stop();
        _lifetime.Cancel();
        _lifetime.Dispose();
        PersistSettings();
        if (_source is { IsDisposed: false } && _hotkeyId != 0)
            Native.UnregisterHotKey(_source.Handle, _hotkeyId);
        _overlay.Close();
        _tray.Visible = false;
        _tray.ContextMenuStrip?.Dispose();
        _tray.Dispose();
        _trayIcon.Dispose();
        base.OnClosed(e);
    }
}
