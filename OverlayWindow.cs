using System;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Screen = System.Windows.Forms.Screen;

namespace Prism;

internal sealed class OverlayWindow : Window
{
    private readonly OverlayView _view = new() { IsHitTestVisible = false };
    private readonly Settings _settings;
    private readonly Native.WinEventProc _foregroundChanged;
    private IntPtr _handle;
    private IntPtr _hook;
    private bool _placementQueued;

    internal OverlayWindow(Settings settings)
    {
        _settings = settings;
        _foregroundChanged = (_, eventType, _, _, _, _, _) =>
        {
            if (eventType is Native.ForegroundEvent or Native.MoveSizeEndEvent)
                QueuePlacement();
        };
        Title = "Prism Crosshair Overlay";
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        Width = Height = 160;
        ShowActivated = false;
        ShowInTaskbar = false;
        Focusable = false;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        Topmost = true;
        Content = _view;
        SourceInitialized += (_, _) => Native.MakeOverlay(_handle = new WindowInteropHelper(this).Handle);
        DpiChanged += (_, _) =>
        {
            _view.InvalidateVisual();
            QueuePlacement();
        };
    }

    internal void UpdateReticle()
    {
        _view.Reticle = _settings.Reticle;
        _view.InvalidateVisual();
        QueuePlacement();
    }

    internal void SetEnabled(bool enabled)
    {
        if (enabled)
        {
            UpdateReticle();
            if (!IsVisible)
            {
                new WindowInteropHelper(this).EnsureHandle();
                Position();
                Show();
                QueuePlacement();
            }
            RefreshTracking();
        }
        else
        {
            StopTracking();
            Hide();
        }
    }

    internal void RefreshTracking()
    {
        StopTracking();
        if (IsVisible && _settings.Placement == PlacementMode.ActiveMonitor)
        {
            _hook = Native.SetWinEventHook(Native.ForegroundEvent, Native.MoveSizeEndEvent, IntPtr.Zero,
                _foregroundChanged, 0, 0, 0);
        }
        QueuePlacement();
    }

    private void StopTracking()
    {
        if (_hook == IntPtr.Zero)
            return;

        Native.UnhookWinEvent(_hook);
        _hook = IntPtr.Zero;
    }

    internal void QueuePlacement()
    {
        if (_placementQueued || !IsVisible)
            return;

        _placementQueued = true;
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
        {
            _placementQueued = false;
            if (IsVisible)
                Position();
        }));
    }

    private void Position()
    {
        var foreground = Native.GetForegroundWindow();
        var bounds = _settings.Placement == PlacementMode.FixedMonitor
            ? Array.Find(Screen.AllScreens, screen => screen.DeviceName == _settings.MonitorDevice)?.Bounds
                ?? Screen.PrimaryScreen?.Bounds
            : Screen.FromHandle(foreground).Bounds;

        if (bounds is not { } screenBounds || !Native.GetWindowRect(_handle, out var window))
            return;

        var x = screenBounds.Left + screenBounds.Width / 2 + _settings.Reticle.OffsetX;
        var y = screenBounds.Top + screenBounds.Height / 2 + _settings.Reticle.OffsetY;
        Native.PlaceOverlay(_handle, x - (window.Right - window.Left) / 2, y - (window.Bottom - window.Top) / 2);
    }

    protected override void OnClosed(EventArgs e)
    {
        StopTracking();
        base.OnClosed(e);
    }
}
