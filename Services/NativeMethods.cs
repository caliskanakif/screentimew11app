using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace ScreenTimeTracker.Services;

public static class NativeMethods
{
    [StructLayout(LayoutKind.Sequential)]
    struct LASTINPUTINFO { public uint cbSize; public uint dwTime; }

    [StructLayout(LayoutKind.Sequential)]
    struct MARGINS { public int cxLeftWidth, cxRightWidth, cyTopHeight, cyBottomHeight; }

    [DllImport("user32.dll")] static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);
    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
    [DllImport("user32.dll")] static extern int GetWindowLong(IntPtr hWnd, int nIndex);
    [DllImport("user32.dll")] static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
    [DllImport("dwmapi.dll")] static extern int DwmExtendFrameIntoClientArea(IntPtr hwnd, ref MARGINS margins);
    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    /// <summary>Seconds since the last keyboard/mouse input (Windows idle API).</summary>
    public static double GetIdleSeconds()
    {
        var info = new LASTINPUTINFO { cbSize = (uint)Marshal.SizeOf<LASTINPUTINFO>() };
        if (!GetLastInputInfo(ref info)) return 0;
        uint idleMs = unchecked((uint)Environment.TickCount - info.dwTime);
        return idleMs / 1000.0;
    }

    static readonly Dictionary<uint, (string name, long stamp)> Cache = new();

    /// <summary>Process name of the window in the foreground (e.g. "chrome", "Code", "eldenring").</summary>
    public static string? GetForegroundApp()
    {
        var h = GetForegroundWindow();
        if (h == IntPtr.Zero) return null;
        GetWindowThreadProcessId(h, out uint pid);
        if (pid == 0) return null;

        long now = Environment.TickCount64;
        if (Cache.TryGetValue(pid, out var hit) && now - hit.stamp < 60_000) return hit.name;

        string name;
        try { using var p = Process.GetProcessById((int)pid); name = p.ProcessName; }
        catch { name = "Unknown"; }
        if (Cache.Count > 256) Cache.Clear();
        Cache[pid] = (name, now);
        return name;
    }

    /// <summary>Hides the overlay from Alt+Tab and stops it from stealing focus.</summary>
    public static void MakeToolWindow(Window w)
    {
        var h = new WindowInteropHelper(w).Handle;
        int ex = GetWindowLong(h, -20);
        SetWindowLong(h, -20, ex | 0x00000080 | 0x08000000); // WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE
    }

    /// <summary>Windows 11 acrylic backdrop + dark title bar for the dashboard. Fails silently on older systems.</summary>
    public static void ApplyGlass(Window w)
    {
        try
        {
            var h = new WindowInteropHelper(w).Handle;
            var src = HwndSource.FromHwnd(h);
            if (src?.CompositionTarget != null) src.CompositionTarget.BackgroundColor = Colors.Transparent;
            var m = new MARGINS { cxLeftWidth = -1, cxRightWidth = -1, cyTopHeight = -1, cyBottomHeight = -1 };
            DwmExtendFrameIntoClientArea(h, ref m);
            int dark = 1;
            DwmSetWindowAttribute(h, 20, ref dark, 4);      // DWMWA_USE_IMMERSIVE_DARK_MODE
            int backdrop = 3;
            DwmSetWindowAttribute(h, 38, ref backdrop, 4);  // DWMWA_SYSTEMBACKDROP_TYPE = acrylic
        }
        catch { }
    }
}
