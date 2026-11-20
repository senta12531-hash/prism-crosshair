using System;
using System.Threading;
using System.Windows;

namespace Prism;

public partial class App : Application
{
    private Mutex? _instance;
    private bool _ownsInstance;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        _instance = new Mutex(true, "Local\\PrismCrosshair_1514AC38", out _ownsInstance);
        if (!_ownsInstance)
        {
            var existing = Native.FindWindow(null, Prism.MainWindow.WindowTitle);
            if (existing != IntPtr.Zero)
                Native.PostMessage(existing, Native.ShowEditorMessage, IntPtr.Zero, IntPtr.Zero);
            Shutdown();
            return;
        }

        MainWindow = new MainWindow();
        MainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (_ownsInstance)
            _instance?.ReleaseMutex();
        _instance?.Dispose();
        base.OnExit(e);
    }
}
