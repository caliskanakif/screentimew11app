using System.IO;
using System.Reflection;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ScreenTimeTracker.Services;

/// <summary>Loads the embedded app.ico for the tray icon and the dashboard window.</summary>
public static class AppIcon
{
    static Stream? Open() => Assembly.GetExecutingAssembly().GetManifestResourceStream("app.ico");

    public static System.Drawing.Icon? TrayIcon()
    {
        try
        {
            using var s = Open();
            if (s == null) return null;
            return new System.Drawing.Icon(s, System.Windows.Forms.SystemInformation.SmallIconSize);
        }
        catch { return null; }
    }

    public static ImageSource? Image()
    {
        try
        {
            using var s = Open();
            if (s == null) return null;
            var dec = new IconBitmapDecoder(s, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
            var best = dec.Frames.OrderByDescending(f => f.PixelWidth).First();
            best.Freeze();
            return best;
        }
        catch { return null; }
    }
}
