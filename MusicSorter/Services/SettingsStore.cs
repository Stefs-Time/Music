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
        try
        {
            File.WriteAllText(_path,
                JsonSerializer.Serialize(s, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { /* don't crash on settings save */ }
    }
}
