using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Threading;

namespace MusicSorter;

public partial class App : Application
{
    private static readonly string ErrorLogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "MusicSorter", "last-error.log");

    public App()
    {
        // Three layers because no single hook catches everything in WPF.
        DispatcherUnhandledException += (_, e) =>
        {
            ReportFatal("DispatcherUnhandledException", e.Exception);
            e.Handled = true;
        };

        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            ReportFatal("AppDomain.UnhandledException", e.ExceptionObject as Exception);
        };

        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            ReportFatal("UnobservedTaskException", e.Exception);
            e.SetObserved();
        };
    }

    /// <summary>
    /// Anything thrown from base.OnStartup (XAML parse, MainWindow ctor, resource lookup)
    /// is surfaced here before we silently die.
    /// </summary>
    protected override void OnStartup(StartupEventArgs e)
    {
        try
        {
            base.OnStartup(e);
        }
        catch (Exception ex)
        {
            ReportFatal("Application.OnStartup", ex);
            Shutdown(1);
        }
    }

    private static void ReportFatal(string source, Exception? ex)
    {
        // Always write the full chain to a file first — that's the source of truth
        // even if the message box is dismissed too quickly.
        string logPath = ErrorLogPath;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
            File.WriteAllText(logPath, Format(source, ex));
        }
        catch
        {
            // If %APPDATA% is unwritable for some reason, fall back to a temp file
            // and overwrite logPath so the message still points somewhere real.
            try
            {
                logPath = Path.Combine(Path.GetTempPath(), "MusicSorter-last-error.log");
                File.WriteAllText(logPath, Format(source, ex));
            }
            catch { }
        }

        try
        {
            var summary = $"Music Sorter hit an unhandled error and is closing.\n\n" +
                          $"Type    : {ex?.GetType().FullName ?? "(null)"}\n" +
                          $"Message : {ex?.Message ?? "(no message)"}\n";
            if (ex?.InnerException != null)
                summary += $"Inner   : {ex.InnerException.GetType().Name}: {ex.InnerException.Message}\n";
            summary += $"\nFull stack written to:\n{logPath}";

            MessageBox.Show(summary, "Music Sorter — Fatal error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        catch
        {
            // Last-resort fallback if the message box itself can't show.
        }
    }

    private static string Format(string source, Exception? ex)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Music Sorter — {DateTime.Now:O}");
        sb.AppendLine($"Source : {source}");
        sb.AppendLine($"OS     : {Environment.OSVersion}");
        sb.AppendLine($"CLR    : {Environment.Version}");
        sb.AppendLine($"AppDir : {AppContext.BaseDirectory}");
        sb.AppendLine();

        var current = ex;
        int depth = 0;
        while (current != null)
        {
            sb.AppendLine($"=== Exception #{depth} : {current.GetType().FullName} ===");
            sb.AppendLine(current.Message);
            sb.AppendLine();
            if (!string.IsNullOrEmpty(current.StackTrace))
            {
                sb.AppendLine(current.StackTrace);
                sb.AppendLine();
            }
            current = current.InnerException;
            depth++;
        }
        if (depth == 0) sb.AppendLine("(no exception object)");
        return sb.ToString();
    }
}
