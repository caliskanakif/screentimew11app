namespace ScreenTimeTracker.Models;

public sealed class Threshold
{
    public int Minutes { get; set; }
    public string Color { get; set; } = "#FFFFFF";
}

public sealed class AppSettings
{
    // ---- Idle detection / day handling ----
    public int IdleTimeoutSeconds { get; set; } = 60;      // 0 = never idle
    public string ResetTime { get; set; } = "00:00";       // HH:mm, saved in settings.json

    // ---- Text ----
    public string FontFamily { get; set; } = "Segoe UI";
    public double FontSize { get; set; } = 24;
    public string FontWeight { get; set; } = "Regular";    // Light | Regular | SemiBold | Bold
    public string FontColor { get; set; } = "#FFFFFF";
    public bool ShowSeconds { get; set; } = true;          // false = HH:MM

    // ---- Background & shape ----
    public string BackgroundColor { get; set; } = "#000000";
    public double BackgroundOpacity { get; set; } = 1.0;   // 0 = invisible ... 1 = fully opaque
    public bool GlassEffect { get; set; } = false;         // soft gradient "liquid glass" look
    public double CornerPercent { get; set; } = 65;        // 0 = square ... 100 = full pill
    public double PaddingH { get; set; } = 18;
    public double PaddingV { get; set; } = 8;
    public double Opacity { get; set; } = 1.0;             // whole overlay incl. text (0.2 .. 1)

    // ---- Border & shadow ----
    public bool BorderEnabled { get; set; } = false;
    public string BorderColor { get; set; } = "#55FFFFFF";
    public double BorderWidth { get; set; } = 1.2;
    public bool ShadowEnabled { get; set; } = true;
    public double ShadowStrength { get; set; } = 50;       // 0 .. 100

    public List<Threshold> Thresholds { get; set; } = new()
    {
        new() { Minutes = 120, Color = "#FFD740" },
        new() { Minutes = 180, Color = "#FF5252" }
    };

    // ---- Overlay state ----
    public bool OverlayVisible { get; set; } = true;
    public double OverlayLeft { get; set; } = 100;
    public double OverlayTop { get; set; } = 100;

    // ---- Cloud sync (folder inside "Google Drive for desktop"; empty = off) ----
    public string SyncFolder { get; set; } = "";
    public string DeviceId { get; set; } = "";
    public string DeviceName { get; set; } = "";

    // ---- Dashboard state ----
    public string StatsRange { get; set; } = "Week";       // Day | Week | Month | Year | All
    public string StatsChart { get; set; } = "Bar";        // Bar | Line | Pie

    // ---- Focus rules (transparent + editable): share of active time spent in these apps ----
    public string FocusApps { get; set; } = "Code,devenv,rider64,pycharm64,idea64,WINWORD,EXCEL,POWERPNT,notepad,obsidian,Notion";
    public int FocusHighPercent { get; set; } = 60;
    public int FocusLowPercent { get; set; } = 30;
}

public sealed class DayRecord
{
    public double ActiveSeconds { get; set; }
    public double IdleSeconds { get; set; }
    public Dictionary<string, double> Apps { get; set; } = new();
    public double[] Hours { get; set; } = new double[24];

    public void Normalize()
    {
        Apps ??= new();
        if (Hours == null || Hours.Length != 24) Hours = new double[24];
    }
}
