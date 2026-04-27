using System.IO;
using System.Runtime.CompilerServices;

namespace MusicSorter;

/// <summary>
/// Runs the moment our assembly is loaded by the runtime — *before* App's constructor,
/// before WPF, before anything we wrote. Writes a one-line breadcrumb every time it
/// passes a phase so a startup crash leaves a trail in
/// <c>%APPDATA%\MusicSorter\startup.log</c>.
/// </summary>
internal static class StartupTrace
{
    private static readonly string LogPath = ResolvePath();

    private static string ResolvePath()
    {
        try
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "MusicSorter");
            Directory.CreateDirectory(dir);
            return Path.Combine(dir, "startup.log");
        }
        catch
        {
            try { return Path.Combine(Path.GetTempPath(), "MusicSorter-startup.log"); }
            catch { return ""; }
        }
    }

    [ModuleInitializer]
    public static void Init()
    {
        // First line resets the file so we don't accumulate run-after-run.
        Reset();
        Log("module initializer reached");
    }

    private static void Reset()
    {
        try
        {
            if (!string.IsNullOrEmpty(LogPath))
                File.WriteAllText(LogPath, "");
        }
        catch { }
    }

    public static void Log(string message)
    {
        try
        {
            if (string.IsNullOrEmpty(LogPath)) return;
            File.AppendAllText(LogPath,
                $"{DateTime.Now:HH:mm:ss.fff} {message}{Environment.NewLine}");
        }
        catch { }
    }
}
