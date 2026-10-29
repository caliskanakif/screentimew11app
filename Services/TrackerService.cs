using System.Windows.Threading;
using ScreenTimeTracker.Models;

namespace ScreenTimeTracker.Services;

/// <summary>
/// The tracking engine. Runs independently of any window: closing the dashboard never stops tracking.
///
/// Persistence: every tracking day is its own file (days\yyyy-MM-dd.json) and is kept forever.
/// Today's file is written every 10 seconds, on suspend, on logoff/shutdown and on exit, so after a
/// restart the timer continues exactly where it was until the daily reset time is reached.
///
/// Sleep safety: every tick measures real elapsed time. If more than 3 seconds passed since the last
/// tick (PC was asleep, app was suspended, ...), that gap is NOT counted.
/// </summary>
public sealed class TrackerService : IDisposable
{
    public AppSettings Settings { get; }
    public Dictionary<string, DayRecord> Days { get; }

    public SyncExportService Sync { get; }

    public bool Paused { get; set; }
    public bool IsIdle { get; private set; }
    public string CurrentApp { get; private set; } = "";

    /// <summary>Raised every second (UI thread).</summary>
    public event Action? Updated;

    readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1) };
    readonly HashSet<string> _dirty = new();
    DateTime _last = DateTime.Now;
    int _saveCounter;

    public TrackerService()
    {
        Settings = Storage.Load<AppSettings>("settings.json") ?? new AppSettings();
        Settings.Thresholds ??= new();
        Days = Storage.LoadDays();
        Sync = new SyncExportService(this);
        _timer.Tick += (_, _) => Tick();
    }

    public void Start() { _last = DateTime.Now; _timer.Start(); }
    public void SkipGap() { _last = DateTime.Now; }
    public void Refresh() => Updated?.Invoke();

    // ----- tracking days (configurable reset time) -----
    TimeSpan ResetSpan =>
        TimeSpan.TryParse(Settings.ResetTime, out var t) && t >= TimeSpan.Zero && t < TimeSpan.FromDays(1)
            ? t : TimeSpan.Zero;

    /// <summary>With a reset time of 04:00, 02:00 at night still belongs to the previous day.</summary>
    public DateTime TrackingDate(DateTime now) => (now - ResetSpan).Date;
    public static string KeyFor(DateTime date) => date.ToString("yyyy-MM-dd");
    public string TodayKey => KeyFor(TrackingDate(DateTime.Now));

    public DayRecord Today => GetDay(TodayKey);
    public DayRecord GetDay(string key)
    {
        if (!Days.TryGetValue(key, out var r)) { r = new DayRecord(); Days[key] = r; }
        return r;
    }
    public DayRecord? FindDay(DateTime date) => Days.TryGetValue(KeyFor(date), out var r) ? r : null;

    void Tick()
    {
        var now = DateTime.Now;
        double delta = (now - _last).TotalSeconds;
        _last = now;

        if (delta <= 0 || delta > 3)           // sleep / stall / clock change -> ignore the gap
        {
            Updated?.Invoke();
            return;
        }

        double idleSecs = NativeMethods.GetIdleSeconds();
        IsIdle = Settings.IdleTimeoutSeconds > 0 && idleSecs >= Settings.IdleTimeoutSeconds;
        CurrentApp = IsIdle ? "" : (NativeMethods.GetForegroundApp() ?? "");

        if (!Paused)
        {
            var key = TodayKey;
            var rec = GetDay(key);
            if (IsIdle)
            {
                rec.IdleSeconds += delta;
            }
            else
            {
                rec.ActiveSeconds += delta;
                rec.Hours[now.Hour] += delta;
                if (CurrentApp.Length > 0)
                    rec.Apps[CurrentApp] = rec.Apps.GetValueOrDefault(CurrentApp) + delta;
            }
            _dirty.Add(key);
        }

        Updated?.Invoke();
        if (++_saveCounter >= 10) { _saveCounter = 0; Save(); Sync.FlushIfDue(); }
    }

    public void ResetToday()
    {
        var key = TodayKey;
        Days[key] = new DayRecord();
        _dirty.Add(key);
        Save();
        Updated?.Invoke();
    }

    public void SaveSettings() => Storage.Save("settings.json", Settings);

    public void Save()
    {
        Storage.Save("settings.json", Settings);
        foreach (var key in _dirty.ToList())
            if (Days.TryGetValue(key, out var rec)) { Storage.SaveDay(key, rec); Sync.Queue(key); }
        _dirty.Clear();
    }

    public void Dispose()
    {
        _timer.Stop();
        Save();
        Sync.FlushIfDue(force: true);
    }
}
