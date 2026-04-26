using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using MusicSorter.Models;
using MusicSorter.Services;

namespace MusicSorter;

public partial class MainWindow : Window
{
    private CancellationTokenSource? _cts;
    private readonly SettingsStore _settings = new();

    public MainWindow()
    {
        InitializeComponent();

        LayoutCombo.ItemsSource = FolderLayoutOptions.Layouts;
        LayoutCombo.DisplayMemberPath = nameof(FolderLayoutOption.Label);
        LayoutCombo.SelectedIndex = 0;

        FilenameCombo.ItemsSource = FolderLayoutOptions.FileNames;
        FilenameCombo.DisplayMemberPath = nameof(FileNameOption.Label);
        FilenameCombo.SelectedIndex = 0;

        ShazamHostCombo.ItemsSource = new[]
        {
            "shazam.p.rapidapi.com",
            "shazam-core.p.rapidapi.com",
            "shazam-api6.p.rapidapi.com"
        };
        ShazamHostCombo.SelectedIndex = 0;

        Loaded += (_, _) => LoadSettings();
        Closing += (_, _) => SaveSettings();
    }

    private void LoadSettings()
    {
        var s = _settings.Load();
        SourceBox.Text = s.Source ?? "";
        OutputBox.Text = s.Output ?? "";
        AcoustIdKeyBox.Text = s.AcoustIdKey ?? "";
        ShazamKeyBox.Text = s.ShazamKey ?? "";
        FpcalcBox.Text = s.FpcalcPath ?? "";
        if (!string.IsNullOrEmpty(s.ShazamHost))
            ShazamHostCombo.Text = s.ShazamHost;
        if (s.LayoutIndex is int li && li >= 0 && li < FolderLayoutOptions.Layouts.Count)
            LayoutCombo.SelectedIndex = li;
        if (s.FileNameIndex is int fi && fi >= 0 && fi < FolderLayoutOptions.FileNames.Count)
            FilenameCombo.SelectedIndex = fi;
        ChkClean.IsChecked = s.UseClean ?? true;
        ChkMusicBrainz.IsChecked = s.UseMusicBrainz ?? true;
        ChkAcoustId.IsChecked = s.UseAcoustId ?? true;
        ChkShazam.IsChecked = s.UseShazam ?? true;
        ChkCoverArt.IsChecked = s.UseCoverArt ?? true;
        ChkOverwriteTags.IsChecked = s.OverwriteTags ?? true;
        ChkSkipExisting.IsChecked = s.SkipExisting ?? true;
        RbMove.IsChecked = !(s.Copy ?? false);
        RbCopy.IsChecked = s.Copy ?? false;
    }

    private void SaveSettings()
    {
        _settings.Save(new AppSettings
        {
            Source = SourceBox.Text,
            Output = OutputBox.Text,
            AcoustIdKey = AcoustIdKeyBox.Text,
            ShazamKey = ShazamKeyBox.Text,
            FpcalcPath = FpcalcBox.Text,
            ShazamHost = ShazamHostCombo.Text,
            LayoutIndex = LayoutCombo.SelectedIndex,
            FileNameIndex = FilenameCombo.SelectedIndex,
            UseClean = ChkClean.IsChecked == true,
            UseMusicBrainz = ChkMusicBrainz.IsChecked == true,
            UseAcoustId = ChkAcoustId.IsChecked == true,
            UseShazam = ChkShazam.IsChecked == true,
            UseCoverArt = ChkCoverArt.IsChecked == true,
            OverwriteTags = ChkOverwriteTags.IsChecked == true,
            SkipExisting = ChkSkipExisting.IsChecked == true,
            Copy = RbCopy.IsChecked == true
        });
    }

    private void BrowseSource_Click(object sender, RoutedEventArgs e) => PickFolder(SourceBox);
    private void BrowseOutput_Click(object sender, RoutedEventArgs e) => PickFolder(OutputBox);

    private static void PickFolder(System.Windows.Controls.TextBox target)
    {
        var dlg = new OpenFolderDialog
        {
            Title = "Select a folder",
            InitialDirectory = Directory.Exists(target.Text) ? target.Text : ""
        };
        if (dlg.ShowDialog() == true)
            target.Text = dlg.FolderName;
    }

    private async void StartBtn_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(SourceBox.Text) || !Directory.Exists(SourceBox.Text))
        {
            System.Windows.MessageBox.Show("Please choose a valid source folder.", "Music Sorter",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        if (string.IsNullOrWhiteSpace(OutputBox.Text))
        {
            System.Windows.MessageBox.Show("Please choose an output folder.", "Music Sorter",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        Directory.CreateDirectory(OutputBox.Text);

        SaveSettings();

        var opts = new SortOptions
        {
            Source = SourceBox.Text,
            Output = OutputBox.Text,
            Layout = ((FolderLayoutOption)LayoutCombo.SelectedItem!).Layout,
            FileName = ((FileNameOption)FilenameCombo.SelectedItem!).Pattern,
            UseClean = ChkClean.IsChecked == true,
            UseMusicBrainz = ChkMusicBrainz.IsChecked == true,
            UseAcoustId = ChkAcoustId.IsChecked == true,
            UseShazam = ChkShazam.IsChecked == true,
            UseCoverArt = ChkCoverArt.IsChecked == true,
            OverwriteTags = ChkOverwriteTags.IsChecked == true,
            SkipExisting = ChkSkipExisting.IsChecked == true,
            Move = RbMove.IsChecked == true,
            AcoustIdKey = AcoustIdKeyBox.Text?.Trim() ?? "",
            ShazamKey = ShazamKeyBox.Text?.Trim() ?? "",
            ShazamHost = (ShazamHostCombo.Text ?? "").Trim(),
            FpcalcPath = FpcalcBox.Text?.Trim() ?? ""
        };

        StartBtn.IsEnabled = false;
        CancelBtn.IsEnabled = true;
        Progress.Value = 0;
        LogBox.Clear();
        _cts = new CancellationTokenSource();

        var progress = new Progress<SorterProgress>(p =>
        {
            Progress.Value = p.Percent;
            if (!string.IsNullOrEmpty(p.Line))
            {
                LogBox.AppendText(p.Line + Environment.NewLine);
                LogBox.ScrollToEnd();
            }
        });

        try
        {
            var sorter = new Mp3Sorter(opts);
            await Task.Run(() => sorter.RunAsync(progress, _cts.Token), _cts.Token);
            Log("== Done.");
        }
        catch (OperationCanceledException)
        {
            Log("== Cancelled.");
        }
        catch (Exception ex)
        {
            Log($"!! {ex.Message}");
        }
        finally
        {
            StartBtn.IsEnabled = true;
            CancelBtn.IsEnabled = false;
            _cts?.Dispose();
            _cts = null;
        }
    }

    private void CancelBtn_Click(object sender, RoutedEventArgs e)
    {
        _cts?.Cancel();
        Log("== Cancel requested...");
    }

    private void Log(string line)
    {
        LogBox.AppendText(line + Environment.NewLine);
        LogBox.ScrollToEnd();
    }
}
