using System.IO;
using System.Text.Json;
using ScreenTimeTracker.Models;

namespace ScreenTimeTracker.Services;

/// <summary>
/// Local JSON storage in %AppData%\ScreenTimeTracker. Nothing ever leaves the PC.
///   settings.json        all settings (incl. daily reset time)
///   days\yyyy-MM-dd.json one small file per tracking day - kept forever, never deleted
/// Only today's file is rewritten while the app runs, so years of history cost nothing.
/// </summary>
public static class Storage
{
    public static readonly string Dir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ScreenTimeTracker");

    public static string DaysDir => Path.Combine(Dir, "days");

    static readonly JsonSerializerOptions Opt = new() { WriteIndented = true };

    public static T? Load<T>(string relativePath)
    {
        var path = Path.Combine(Dir, relativePath);
        try
        {
            if (!File.Exists(path)) return default;
            return JsonSerializer.Deserialize<T>(File.ReadAllText(path));
        }
        catch
        {
            // keep a copy of an unreadable file instead of silently overwriting it
            try { File.Copy(path, path + ".corrupt", true); } catch { }
            return default;
        }
    }

    public static void Save<T>(string relativePath, T value)
    {
        try
        {
            var path = Path.Combine(Dir, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var tmp = path + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(value, Opt));
            File.Move(tmp, path, true);
        }
        catch { /* never crash because of disk problems */ }
    }

    public static void SaveDay(string key, DayRecord rec) => Save(Path.Combine("days", key + ".json"), rec);

    /// <summary>Loads every stored day. Also migrates the older single-file usage.json once.</summary>
    public static Dictionary<string, DayRecord> LoadDays()
    {
        var result = new Dictionary<string, DayRecord>();
        try
        {
            Directory.CreateDirectory(DaysDir);
            foreach (var f in Directory.GetFiles(DaysDir, "*.json"))
            {
                try
                {
                    var rec = JsonSerializer.Deserialize<DayRecord>(File.ReadAllText(f));
                    if (rec == null) continue;
                    rec.Normalize();
                    result[Path.GetFileNameWithoutExtension(f)] = rec;
                }
                catch
                {
                    try { File.Copy(f, f + ".corrupt", true); } catch { }
                }
            }

            var old = Path.Combine(Dir, "usage.json");
            if (File.Exists(old))
            {
                var legacy = Load<Dictionary<string, DayRecord>>("usage.json");
                if (legacy != null)
                {
                    foreach (var kv in legacy)
                    {
                        kv.Value.Normalize();
                        if (result.ContainsKey(kv.Key)) continue;
                        result[kv.Key] = kv.Value;
                        SaveDay(kv.Key, kv.Value);
                    }
                    File.Move(old, old + ".migrated", true);
                }
            }
        }
        catch { }
        return result;
    }
}
