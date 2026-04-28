using System.IO;
using System.Text.Json;
using MusicSorter.Models;

namespace MusicSorter.Services;

public sealed class SettingsStore
{
    private readonly string _path;

    public SettingsStore()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "MusicSorter");
        Directory.CreateDirectory(dir);
        _path = Path.Combine(dir, "settings.json");
    }

    public AppSettings Load()
    {
        if (!File.Exists(_path)) return new AppSettings();
        try
        {
            return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(_path)) ?? new AppSettings();
        }
        catch
        {
            return new AppSettings();
        }
    }

    public void Save(AppSettings s)
    {
        // Write atomically: serialize to a sibling .tmp first, then rename over the
        // real file. A crash mid-write leaves the previous valid file intact instead
        // of producing a half-written JSON that Load() would silently fall back from.
        try
        {
            var json = JsonSerializer.Serialize(s, new JsonSerializerOptions { WriteIndented = true });
            var tmp = _path + ".tmp";
            File.WriteAllText(tmp, json);
            if (File.Exists(_path)) File.Replace(tmp, _path, destinationBackupFileName: null, ignoreMetadataErrors: true);
            else                    File.Move(tmp, _path);
        }
        catch { /* don't crash on settings save */ }
    }
}
