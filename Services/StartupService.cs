using Microsoft.Win32;

namespace ScreenTimeTracker.Services;

/// <summary>"Start with Windows" via the per-user Run registry key (no admin rights needed).</summary>
public static class StartupService
{
    const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    const string ValueName = "ScreenTimeTracker";

    public static bool IsEnabled
    {
        get
        {
            try
            {
                using var k = Registry.CurrentUser.OpenSubKey(RunKey);
                return k?.GetValue(ValueName) is not null;
            }
            catch { return false; }
        }
    }

    public static void Set(bool enabled)
    {
        try
        {
            using var k = Registry.CurrentUser.OpenSubKey(RunKey, true);
            if (k is null) return;
            if (enabled && Environment.ProcessPath is { } exe) k.SetValue(ValueName, "\"" + exe + "\"");
            else k.DeleteValue(ValueName, false);
        }
        catch { }
    }
}
