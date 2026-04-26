using System.Windows;
using System.Windows.Threading;

namespace MusicSorter;

public partial class App : Application
{
    public App()
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            MessageBox.Show(e.ExceptionObject?.ToString() ?? "Unknown error",
                "Fatal error", MessageBoxButton.OK, MessageBoxImage.Error);
        };
    }

    private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        MessageBox.Show(e.Exception.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }
}
