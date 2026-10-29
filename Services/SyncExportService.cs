using System.IO;
using System.Text.Json;
using ScreenTimeTracker.Models;

namespace ScreenTimeTracker.Services;

/// <summary>
/// Writes one small JSON snapshot per tracking day into a "sync folder" (schema v1, see docs/SCHEMA.md):
///     &lt;SyncFolder&gt;\&lt;deviceId&gt;\device.json
///     &lt;SyncFolder&gt;\&lt;deviceId&gt;\yyyy-MM-dd.json
/// Point the sync folder at a folder inside "Google Drive for desktop" and Drive uploads it by itself -
/// the tracker needs no Google login, no token and no internet access of its own.
///
/// The tracking logic is untouched. Files are rewritten as whole-day snapshots (idempotent: writing the same
/// day twice never double counts). Disabled when SyncFolder is empty.
/// </summary>
public sealed class SyncExportService
{
    const int SchemaVersion = 1;
    static readonly TimeSpan MinInterval = TimeSpan.FromMinutes(2);
    static readonly JsonSerializerOptions Opt = new() { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    readonly TrackerService _t;
    readonly HashSet<string> _pending = new();
    readonly object _lock = new();
    DateTime _lastFlush = DateTime.MinValue;

    /// <summary>Human-readable result of the last attempt, shown in the settings.</summary>
    public string Status { get; private set; } = "Sync is off.";
    public bool Enabled => !string.IsNullOrWhiteSpace(_t.Settings.SyncFolder);

    public SyncExportService(TrackerService t)
    {
        _t = t;
        var s = t.Settings;
        if (string.IsNullOrWhiteSpace(s.DeviceId))
        {
            s.DeviceId = "pc-" + Guid.NewGuid().ToString("N")[..8];
            if (string.IsNullOrWhiteSpace(s.DeviceName)) s.DeviceName = Environment.MachineName;
            t.SaveSettings();
        }
        if (Enabled) Status = "Waiting for first sync...";
    }

    public void Queue(string dayKey) { lock (_lock) _pending.Add(dayKey); }

    /// <summary>Re-sends the complete history (used when sync is switched on or the folder changes).</summary>
    public void QueueAll()
    {
        lock (_lock) foreach (var k in _t.Days.Keys) _pending.Add(k);
        _lastFlush = DateTime.MinValue;
    }

    public void FlushIfDue(bool force = false)
    {
        if (!Enabled) { Status = "Sync is off."; return; }
        if (!force && DateTime.Now - _lastFlush < MinInterval) return;
        _lastFlush = DateTime.Now;

        // Serialize on the calling (UI) thread so we never read data while it is being modified.
        List<(string key, string json)> jobs = new();
        string deviceJson;
        lock (_lock)
        {
            foreach (var key in _pending)
            {
                if (!_t.Days.TryGetValue(key, out var rec)) continue;
                bool empty = rec.ActiveSeconds <= 0 && rec.IdleSeconds <= 0;
                // nothing measured: don't create empty files - but do overwrite an existing one (e.g. after "Reset today")
                if (empty && !File.Exists(Path.Combine(_t.Settings.SyncFolder, _t.Settings.DeviceId, key + ".json"))) continue;
                jobs.Add((key, JsonSerializer.Serialize(BuildDay(key, rec), Opt)));
            }
            _pending.Clear();
        }
        deviceJson = JsonSerializer.Serialize(BuildDevice(), Opt);
        var root = _t.Settings.SyncFolder;
        var id = _t.Settings.DeviceId;

        var work = () => Write(root, id, deviceJson, jobs);
        if (force) work(); else Task.Run(work);
    }

    void Write(string root, string id, string deviceJson, List<(string key, string json)> jobs)
    {
        try
        {
            var dir = Path.Combine(root, id);
            Directory.CreateDirectory(dir);
            WriteAtomic(Path.Combine(dir, "device.json"), deviceJson);
            foreach (var (key, json) in jobs) WriteAtomic(Path.Combine(dir, key + ".json"), json);
            Status = $"Last sync {DateTime.Now:HH:mm:ss} ({jobs.Count} day file(s)) -> {dir}";
        }
        catch (Exception ex)
        {
            // keep the days queued so the next attempt retries them
            lock (_lock) foreach (var (key, _) in jobs) _pending.Add(key);
            Status = "Sync failed (will retry): " + ex.Message;
        }
    }

    static void WriteAtomic(string path, string content)
    {
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, content);
        File.Move(tmp, path, true);
    }

    object BuildDevice() => new
    {
        schema = SchemaVersion,
        id = _t.Settings.DeviceId,
        name = string.IsNullOrWhiteSpace(_t.Settings.DeviceName) ? Environment.MachineName : _t.Settings.DeviceName,
        platform = "windows",
        source = "ScreenTimeTracker",
        sourceVersion = "3.0",
        updatedAt = DateTime.UtcNow.ToString("o")
    };

    object BuildDay(string key, DayRecord rec)
    {
        double total = rec.ActiveSeconds;
        return new
        {
            schema = SchemaVersion,
            deviceId = _t.Settings.DeviceId,
            date = key,
            timeZone = TimeZoneInfo.Local.Id,
            resetTime = _t.Settings.ResetTime,
            updatedAt = DateTime.UtcNow.ToString("o"),
            kind = "measured",
            // Definition: seconds with keyboard/mouse input in the last <idleTimeoutSeconds>, PC awake, not paused.
            measurement = new { method = "windows-input-activity", idleTimeoutSeconds = _t.Settings.IdleTimeoutSeconds },
            totalSeconds = Math.Round(total),
            idleSeconds = Math.Round(rec.IdleSeconds),
            hours = rec.Hours.Select(h => Math.Round(h)).ToArray(),
            apps = rec.Apps.Where(a => a.Value >= 1).ToDictionary(a => a.Key, a => Math.Round(a.Value))
        };
    }
}
