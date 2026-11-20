using System;
using System.Runtime.InteropServices;

namespace Prism;

internal static class Native
{
    internal const int ShowEditorMessage = 0x8001;
    internal const int HotkeyMessage = 0x0312;
    internal const int DisplayChangeMessage = 0x007E;
    internal const int ForegroundEvent = 0x0003;
    internal const int MoveSizeEndEvent = 0x000B;
    private const int ExtendedStyle = -20;
    private const int Transparent = 0x20;
    private const int ToolWindow = 0x80;
    private const int NoActivate = 0x08000000;

    internal static void MakeOverlay(IntPtr window) =>
        SetWindowLong(window, ExtendedStyle, GetWindowLong(window, ExtendedStyle) |
            Transparent | ToolWindow | NoActivate);

    internal static void PlaceOverlay(IntPtr window, int x, int y) =>
        SetWindowPos(window, new IntPtr(-1), x, y, 0, 0, 0x0001 | 0x0010 | 0x0200);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
    private static extern int GetWindowLong(IntPtr hwnd, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongW")]
    private static extern int SetWindowLong(IntPtr hwnd, int index, int value);

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hwnd, IntPtr insertAfter, int x, int y,
        int width, int height, uint flags);

    [DllImport("user32.dll")]
    internal static extern bool GetWindowRect(IntPtr hwnd, out Rect rect);

    [StructLayout(LayoutKind.Sequential)]
    internal struct Rect
    {
        internal int Left, Top, Right, Bottom;
    }

    [DllImport("user32.dll")]
    internal static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", EntryPoint = "FindWindowW", CharSet = CharSet.Unicode)]
    internal static extern IntPtr FindWindow(string? className, string windowName);

    [DllImport("user32.dll", EntryPoint = "PostMessageW")]
    internal static extern bool PostMessage(IntPtr window, int message, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern bool RegisterHotKey(IntPtr hwnd, int id, uint modifiers, uint vKey);

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern bool UnregisterHotKey(IntPtr hwnd, int id);

    internal delegate void WinEventProc(IntPtr hook, uint eventType, IntPtr window,
        int objectId, int childId, uint threadId, uint eventTime);

    [DllImport("user32.dll")]
    internal static extern IntPtr SetWinEventHook(uint eventMin, uint eventMax, IntPtr module,
        WinEventProc callback, uint process, uint thread, uint flags);

    [DllImport("user32.dll")]
    internal static extern bool UnhookWinEvent(IntPtr hook);
}
