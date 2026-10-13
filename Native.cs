using System;
using System.Management;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Island;

internal static class Native
{
    public const int GWL_EXSTYLE = -20;
    public const int WS_EX_TOOLWINDOW = 0x80;
    public const int WM_CLIPBOARDUPDATE = 0x031D;

    [DllImport("user32.dll")] public static extern int GetWindowLong(IntPtr hWnd, int nIndex);
    [DllImport("user32.dll")] public static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
    [DllImport("user32.dll")] public static extern bool AddClipboardFormatListener(IntPtr hwnd);
    [DllImport("user32.dll")] public static extern bool LockWorkStation();
    [DllImport("powrprof.dll")] public static extern bool SetSuspendState(bool hibernate, bool forceCritical, bool disableWakeEvent);

    [StructLayout(LayoutKind.Sequential)] public struct CursorPoint { public int X, Y; }
    [DllImport("user32.dll")] public static extern bool GetCursorPos(out CursorPoint point);

    [DllImport("user32.dll")] private static extern IntPtr GetDC(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern int ReleaseDC(IntPtr hwnd, IntPtr hdc);
    [DllImport("gdi32.dll")] private static extern int GetDeviceCaps(IntPtr hdc, int index);

    public static int GetRefreshRate()
    {
        var hdc = GetDC(IntPtr.Zero);
        int hz = GetDeviceCaps(hdc, 116 /* VREFRESH */);
        ReleaseDC(IntPtr.Zero, hdc);
        return hz is >= 30 and <= 360 ? hz : 60;
    }

    // ---------- stats ----------

    [DllImport("kernel32.dll")] public static extern bool GetSystemTimes(out long idle, out long kernel, out long user);

    [StructLayout(LayoutKind.Sequential)]
    public struct MemoryStatus
    {
        public uint Length, MemoryLoad;
        public ulong TotalPhys, AvailPhys, TotalPageFile, AvailPageFile, TotalVirtual, AvailVirtual, AvailExtendedVirtual;
    }
    [DllImport("kernel32.dll")] public static extern bool GlobalMemoryStatusEx(ref MemoryStatus status);

    [StructLayout(LayoutKind.Sequential)]
    public struct PowerStatus
    {
        public byte ACLineStatus, BatteryFlag, BatteryLifePercent, SystemStatusFlag;
        public int BatteryLifeTime, BatteryFullLifeTime;
    }
    [DllImport("kernel32.dll")] public static extern bool GetSystemPowerStatus(out PowerStatus status);

    // ---------- wallpaper ----------

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool SystemParametersInfo(int action, int param, string? value, int flags);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "SystemParametersInfoW")]
    private static extern bool SystemParametersInfoGet(int action, int param, StringBuilder value, int flags);

    public static void SetWallpaper(string path) => SystemParametersInfo(0x14, 0, path, 0x01 | 0x02);

    public static string GetWallpaper()
    {
        var sb = new StringBuilder(260);
        SystemParametersInfoGet(0x73, sb.Capacity, sb, 0);
        return sb.ToString();
    }

    // ---------- file icons ----------

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ShFileInfo
    {
        public IntPtr hIcon;
        public int iIcon;
        public uint dwAttributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string szDisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)] public string szTypeName;
    }
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SHGetFileInfo(string path, uint attributes, ref ShFileInfo info, uint size, uint flags);
    [DllImport("user32.dll")] private static extern bool DestroyIcon(IntPtr hIcon);

    public static (ImageSource? Icon, string Name) GetFileInfo(string path)
    {
        var info = new ShFileInfo();
        const uint SHGFI_ICON = 0x100, SHGFI_DISPLAYNAME = 0x200;
        SHGetFileInfo(path, 0, ref info, (uint)Marshal.SizeOf<ShFileInfo>(), SHGFI_ICON | SHGFI_DISPLAYNAME);
        ImageSource? icon = null;
        if (info.hIcon != IntPtr.Zero)
        {
            icon = Imaging.CreateBitmapSourceFromHIcon(info.hIcon, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            icon.Freeze();
            DestroyIcon(info.hIcon);
        }
        var name = string.IsNullOrEmpty(info.szDisplayName) ? System.IO.Path.GetFileNameWithoutExtension(path) : info.szDisplayName;
        return (icon, name);
    }
}

// Master volume of the default playback device (Core Audio).
internal static class AudioVolume
{
    [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
    private class MMDeviceEnumerator { }

    [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceEnumerator
    {
        void EnumAudioEndpoints_Unused();
        IMMDevice GetDefaultAudioEndpoint(int dataFlow, int role);
    }

    [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDevice
    {
        [return: MarshalAs(UnmanagedType.IUnknown)]
        object Activate(ref Guid iid, int clsCtx, IntPtr activationParams);
    }

    [ComImport, Guid("5CDF2C82-841E-4546-9722-0CF74078229A"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioEndpointVolume
    {
        void RegisterControlChangeNotify_Unused();
        void UnregisterControlChangeNotify_Unused();
        uint GetChannelCount();
        void SetMasterVolumeLevel(float levelDb, ref Guid context);
        void SetMasterVolumeLevelScalar(float level, ref Guid context);
        float GetMasterVolumeLevel();
        float GetMasterVolumeLevelScalar();
        void SetChannelVolumeLevel(uint channel, float levelDb, ref Guid context);
        void SetChannelVolumeLevelScalar(uint channel, float level, ref Guid context);
        float GetChannelVolumeLevel(uint channel);
        float GetChannelVolumeLevelScalar(uint channel);
        void SetMute([MarshalAs(UnmanagedType.Bool)] bool mute, ref Guid context);
        [return: MarshalAs(UnmanagedType.Bool)] bool GetMute();
    }

    private static IAudioEndpointVolume? Endpoint()
    {
        try
        {
            var enumerator = (IMMDeviceEnumerator)new MMDeviceEnumerator();
            var device = enumerator.GetDefaultAudioEndpoint(0 /* render */, 1 /* multimedia */);
            var iid = typeof(IAudioEndpointVolume).GUID;
            return (IAudioEndpointVolume)device.Activate(ref iid, 23 /* CLSCTX_ALL */, IntPtr.Zero);
        }
        catch { return null; }
    }

    public static (float Level, bool Muted)? Get()
    {
        try
        {
            var e = Endpoint();
            return e is null ? null : (e.GetMasterVolumeLevelScalar(), e.GetMute());
        }
        catch { return null; }
    }

    public static void SetLevel(float level)
    {
        var ctx = Guid.Empty;
        try { Endpoint()?.SetMasterVolumeLevelScalar(Math.Clamp(level, 0f, 1f), ref ctx); } catch { }
    }

    public static void SetMute(bool mute)
    {
        var ctx = Guid.Empty;
        try { Endpoint()?.SetMute(mute, ref ctx); } catch { }
    }
}

// Built-in display brightness via WMI. Works on laptops; most external monitors return null.
internal static class Brightness
{
    public static int? Get()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("root\\WMI", "SELECT CurrentBrightness FROM WmiMonitorBrightness");
            foreach (ManagementObject o in searcher.Get())
                return Convert.ToInt32(o["CurrentBrightness"]);
        }
        catch { }
        return null;
    }

    public static void Set(int value)
    {
        try
        {
            using var cls = new ManagementClass("root\\WMI", "WmiMonitorBrightnessMethods", null);
            foreach (ManagementObject o in cls.GetInstances())
            {
                o.InvokeMethod("WmiSetBrightness", new object[] { 1u, (byte)Math.Clamp(value, 0, 100) });
                break;
            }
        }
        catch { }
    }
}
