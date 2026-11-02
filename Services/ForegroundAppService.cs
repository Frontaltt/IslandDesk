using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;

namespace IslandDesk.Services;

public static class ForegroundAppService
{
    private const uint MonitorDefaultToNearest = 2;

    public static string? GetProcessName()
    {
        try
        {
            IntPtr window = GetForegroundWindow();
            if (window == IntPtr.Zero) return null;
            GetWindowThreadProcessId(window, out uint pid);
            if (pid == 0 || pid == Environment.ProcessId) return null;
            return Process.GetProcessById((int)pid).ProcessName;
        }
        catch { return null; }
    }

    public static bool IsForegroundWindowFullscreen()
    {
        try
        {
            IntPtr window = GetForegroundWindow();
            if (window == IntPtr.Zero || !GetWindowRect(window, out var rect)) return false;
            IntPtr monitor = MonitorFromWindow(window, MonitorDefaultToNearest);
            if (monitor == IntPtr.Zero) return false;
            var info = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
            if (!GetMonitorInfo(monitor, ref info)) return false;

            const int tolerance = 2;
            return Math.Abs(rect.Left - info.rcMonitor.Left) <= tolerance &&
                   Math.Abs(rect.Top - info.rcMonitor.Top) <= tolerance &&
                   Math.Abs(rect.Right - info.rcMonitor.Right) <= tolerance &&
                   Math.Abs(rect.Bottom - info.rcMonitor.Bottom) <= tolerance;
        }
        catch { return false; }
    }

    /// <summary>Bounds and effective DPI for the monitor containing the foreground application.</summary>
    public static (Rectangle Bounds, double ScaleX, double ScaleY)? GetForegroundMonitor()
    {
        try
        {
            IntPtr window = GetForegroundWindow();
            if (window == IntPtr.Zero) return null;
            IntPtr monitor = MonitorFromWindow(window, MonitorDefaultToNearest);
            if (monitor == IntPtr.Zero) return null;
            var info = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
            if (!GetMonitorInfo(monitor, ref info)) return null;

            uint dpiX = 96, dpiY = 96;
            try { _ = GetDpiForMonitor(monitor, 0, out dpiX, out dpiY); } catch { }
            double scaleX = (dpiX > 96u ? dpiX : 96u) / 96d;
            double scaleY = (dpiY > 96u ? dpiY : 96u) / 96d;
            var rect = info.rcMonitor;
            return (Rectangle.FromLTRB(rect.Left, rect.Top, rect.Right, rect.Bottom), scaleX, scaleY);
        }
        catch { return null; }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }

    [DllImport("shcore.dll")] private static extern int GetDpiForMonitor(IntPtr hmonitor, int dpiType, out uint dpiX, out uint dpiY);
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
    [DllImport("user32.dll")] private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);
    [DllImport("user32.dll", CharSet = CharSet.Auto)] private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);
}
