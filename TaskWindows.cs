using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace Brim;

// The windows the Windows taskbar would show, and the actions a taskbar button performs on them.
internal static class TaskWindows
{
    public sealed record Info(IntPtr Handle, string Title, int ProcessId);

    // Shell hook codes (RegisterShellHookWindow).
    public const int HSHELL_WINDOWCREATED = 1, HSHELL_WINDOWDESTROYED = 2, HSHELL_WINDOWACTIVATED = 4,
                     HSHELL_REDRAW = 6, HSHELL_RUDEAPPACTIVATED = 0x8004, HSHELL_FLASH = 0x8006;

    private const int GW_OWNER = 4, GWL_EXSTYLE = -20, GCLP_HICON = -14, GCLP_HICONSM = -34;
    private const int WS_EX_TOOLWINDOW = 0x80, WS_EX_APPWINDOW = 0x40000, WS_EX_NOACTIVATE = 0x08000000;
    private const int DWMWA_CLOAKED = 14;
    private const uint WM_GETICON = 0x7F, WM_CLOSE = 0x10, SMTO_ABORTIFHUNG = 0x2;
    private const int SW_RESTORE = 9, SW_MINIMIZE = 6;

    private delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam);
    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr lParam);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr hwnd);
    [DllImport("user32.dll")] public static extern bool IsWindow(IntPtr hwnd);
    [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern IntPtr GetWindow(IntPtr hwnd, int cmd);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] private static extern IntPtr GetWindowLongPtr(IntPtr hwnd, int index);
    [DllImport("user32.dll", EntryPoint = "GetClassLongPtrW")] private static extern IntPtr GetClassLongPtr(IntPtr hwnd, int index);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowTextLength(IntPtr hwnd);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowText(IntPtr hwnd, StringBuilder text, int max);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out int pid);
    [DllImport("user32.dll")] private static extern IntPtr SendMessageTimeout(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam, uint flags, uint timeout, out IntPtr result);
    [DllImport("user32.dll")] private static extern bool PostMessage(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr hwnd, int cmd);
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr hwnd);
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("dwmapi.dll")] private static extern int DwmGetWindowAttribute(IntPtr hwnd, int attribute, out int value, int size);
    [DllImport("user32.dll")] public static extern bool RegisterShellHookWindow(IntPtr hwnd);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int RegisterWindowMessage(string name);

    // Same rules the taskbar / Alt+Tab use: visible, titled, not a tool window, not owned by another
    // window (unless it asks to be on the taskbar), and not "cloaked" (e.g. on another virtual desktop).
    public static List<Info> List(int ownProcessId)
    {
        var result = new List<Info>();
        EnumWindows((hwnd, _) =>
        {
            if (IsTaskbarWindow(hwnd, ownProcessId, out var info)) result.Add(info);
            return true;
        }, IntPtr.Zero);
        return result;
    }

    private static bool IsTaskbarWindow(IntPtr hwnd, int ownProcessId, out Info info)
    {
        info = null!;
        if (!IsWindowVisible(hwnd)) return false;

        long exStyle = GetWindowLongPtr(hwnd, GWL_EXSTYLE).ToInt64();
        bool appWindow = (exStyle & WS_EX_APPWINDOW) != 0;
        if (!appWindow && ((exStyle & WS_EX_TOOLWINDOW) != 0 || (exStyle & WS_EX_NOACTIVATE) != 0)) return false;
        if (!appWindow && GetWindow(hwnd, GW_OWNER) != IntPtr.Zero) return false;
        if (DwmGetWindowAttribute(hwnd, DWMWA_CLOAKED, out int cloaked, sizeof(int)) == 0 && cloaked != 0) return false;

        GetWindowThreadProcessId(hwnd, out int pid);
        if (pid == ownProcessId) return false;

        var title = TitleOf(hwnd);
        if (title.Length == 0 || title == "Program Manager") return false;

        info = new Info(hwnd, title, pid);
        return true;
    }

    public static string TitleOf(IntPtr hwnd)
    {
        int length = GetWindowTextLength(hwnd);
        if (length <= 0) return "";
        var text = new StringBuilder(length + 1);
        GetWindowText(hwnd, text, text.Capacity);
        return text.ToString();
    }

    // The window's own icon (what the taskbar shows), falling back to its exe's icon.
    public static ImageSource? IconOf(Info window)
    {
        IntPtr icon = IntPtr.Zero;
        foreach (var size in new[] { 1 /* ICON_BIG */, 2 /* ICON_SMALL2 */, 0 /* ICON_SMALL */ })
        {
            if (SendMessageTimeout(window.Handle, WM_GETICON, (IntPtr)size, IntPtr.Zero, SMTO_ABORTIFHUNG, 100, out icon) != IntPtr.Zero && icon != IntPtr.Zero)
                break;
            icon = IntPtr.Zero;
        }
        if (icon == IntPtr.Zero) icon = GetClassLongPtr(window.Handle, GCLP_HICON);
        if (icon == IntPtr.Zero) icon = GetClassLongPtr(window.Handle, GCLP_HICONSM);

        if (icon != IntPtr.Zero)
        {
            try
            {
                // The icon belongs to the window, so it is copied here and never destroyed.
                var image = Imaging.CreateBitmapSourceFromHIcon(icon, Int32Rect.Empty, System.Windows.Media.Imaging.BitmapSizeOptions.FromEmptyOptions());
                image.Freeze();
                return image;
            }
            catch { }
        }
        var path = Native.ProcessPath(window.ProcessId);
        return path == null ? null : Native.GetFileInfo(path).Icon;
    }

    [DllImport("user32.dll")] private static extern bool AttachThreadInput(uint attach, uint attachTo, bool on);
    [DllImport("user32.dll")] private static extern bool BringWindowToTop(IntPtr hwnd);
    [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();

    // Like clicking a taskbar button: bring it to the front, or restore it if minimized.
    public static void Activate(IntPtr hwnd)
    {
        if (IsIconic(hwnd)) ShowWindow(hwnd, SW_RESTORE);
        if (SetForegroundWindow(hwnd) && GetForegroundWindow() == hwnd) return;

        // Windows only lets the app in front hand the foreground to another window. If Brim isn't
        // in front (e.g. driven by keyboard or automation), briefly share input with the app that is.
        uint foregroundThread = GetWindowThreadProcessId(GetForegroundWindow(), out _);
        uint thisThread = GetCurrentThreadId();
        bool attached = foregroundThread != thisThread && AttachThreadInput(thisThread, foregroundThread, true);
        try
        {
            BringWindowToTop(hwnd);
            SetForegroundWindow(hwnd);
        }
        finally
        {
            if (attached) AttachThreadInput(thisThread, foregroundThread, false);
        }
    }

    public static void Minimize(IntPtr hwnd) => ShowWindow(hwnd, SW_MINIMIZE);

    // ---------- full-screen detection ----------

    [StructLayout(LayoutKind.Sequential)] private struct Rect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct MonitorInfo { public int Size; public Rect Monitor, Work; public uint Flags; }
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hwnd, out Rect rect);
    [DllImport("user32.dll")] private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint flags);
    [DllImport("user32.dll")] private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);
    [DllImport("user32.dll")] private static extern bool IsZoomed(IntPtr hwnd);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(IntPtr hwnd, StringBuilder name, int max);

    private static readonly HashSet<string> ShellClasses = new() { "Progman", "WorkerW", "Shell_TrayWnd", "Shell_SecondaryTrayWnd" };

    // True when the window in front covers the whole monitor Brim is on: a full-screen or borderless
    // game, a video in full screen, a slideshow... A plain maximized window (it has a title bar) doesn't
    // count, even when an auto-hiding taskbar lets it reach the screen edges.
    public static bool FullscreenOverMonitorOf(IntPtr own)
    {
        var front = GetForegroundWindow();
        if (front == IntPtr.Zero || front == own) return false;
        GetWindowThreadProcessId(front, out int pid);
        if (pid == Environment.ProcessId) return false; // Brim's own menus and dialogs

        var cls = new StringBuilder(64);
        GetClassName(front, cls, cls.Capacity);
        if (ShellClasses.Contains(cls.ToString())) return false; // the desktop and taskbar

        const uint MONITOR_DEFAULTTONEAREST = 2;
        var monitor = MonitorFromWindow(front, MONITOR_DEFAULTTONEAREST);
        if (monitor != MonitorFromWindow(own, MONITOR_DEFAULTTONEAREST)) return false; // full screen on another monitor

        const long WS_CAPTION = 0x00C00000;
        if (IsZoomed(front) && (GetWindowLongPtr(front, -16 /* GWL_STYLE */).ToInt64() & WS_CAPTION) == WS_CAPTION) return false;

        var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        if (!GetMonitorInfo(monitor, ref info) || !GetWindowRect(front, out var r)) return false;
        return r.Left <= info.Monitor.Left && r.Top <= info.Monitor.Top && r.Right >= info.Monitor.Right && r.Bottom >= info.Monitor.Bottom;
    }

    // Asks the window to close, exactly like its X button (apps can still ask to save).
    public static void Close(IntPtr hwnd) => PostMessage(hwnd, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
}
