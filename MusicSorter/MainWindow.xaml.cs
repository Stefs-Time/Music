using System.IO;
using System.Threading;
using System.Windows;
using Microsoft.Win32;
using MusicSorter.Models;
using MusicSorter.Services;

namespace MusicSorter;

public partial class MainWindow : Window
{
    private CancellationTokenSource? _cts;
    private CancellationTokenSource? _anCts;
    private readonly SettingsStore _settings = new();

    public MainWindow()
    {
        InitializeComponent();

        // Surface the version in the window title — helpful when troubleshooting
        // logs ("which build is this?").
        var ver = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
        if (ver != null) Title = $"Music Sorter {ver.Major}.{ver.Minor}.{ver.Build}";

        LayoutCombo.ItemsSource = FolderLayoutOptions.Layouts;
        LayoutCombo.DisplayMemberPath = nameof(FolderLayoutOption.Label);
        LayoutCombo.SelectedIndex = 0;

        FilenameCombo.ItemsSource = FolderLayoutOptions.FileNames;
        FilenameCombo.DisplayMemberPath = nameof(FileNameOption.Label);
        FilenameCombo.SelectedIndex = 0;

        TagModeCombo.ItemsSource = WriteOptions.TagModes;
        TagModeCombo.DisplayMemberPath = nameof(TagWriteOption.Label);
        TagModeCombo.SelectedIndex = 0;

        ArtModeCombo.ItemsSource = WriteOptions.ArtModes;
        ArtModeCombo.DisplayMemberPath = nameof(CoverArtOption.Label);
        ArtModeCombo.SelectedIndex = 0;

        DupActionCombo.ItemsSource = WriteOptions.DupActions;
        DupActionCombo.DisplayMemberPath = nameof(DuplicateActionOption.Label);
        DupActionCombo.SelectedIndex = 0;

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
        SetIndex(LayoutCombo,    s.LayoutIndex,    FolderLayoutOptions.Layouts.Count);
        SetIndex(FilenameCombo,  s.FileNameIndex,  FolderLayoutOptions.FileNames.Count);
        SetIndex(TagModeCombo,   s.TagModeIndex,   WriteOptions.TagModes.Count);
        SetIndex(ArtModeCombo,   s.ArtModeIndex,   WriteOptions.ArtModes.Count);
        SetIndex(DupActionCombo, s.DupActionIndex, WriteOptions.DupActions.Count);

        ChkClean.IsChecked       = s.UseClean       ?? true;
        ChkMusicBrainz.IsChecked = s.UseMusicBrainz ?? true;
        ChkAcoustId.IsChecked    = s.UseAcoustId    ?? true;
        ChkShazam.IsChecked      = s.UseShazam      ?? true;
        ChkSkipExisting.IsChecked = s.SkipExisting  ?? true;
        RbMove.IsChecked = !(s.Copy ?? false);
        RbCopy.IsChecked =  (s.Copy ?? false);

        ChkDedup.IsChecked    = s.DedupEnabled       ?? true;
        ChkDupAT.IsChecked    = s.DedupByArtistTitle ?? true;
        ChkDupMbid.IsChecked  = s.DedupByMbid        ?? true;
        ChkDupDur.IsChecked   = s.DedupByDuration    ?? true;
        ChkDupHash.IsChecked  = s.DedupByHash        ?? true;
        ChkHashLib.IsChecked  = s.HashEntireLibrary  ?? false;
        ChkDupRecycle.IsChecked = s.DupRecycle       ?? true;
        ChkLetterFallback.IsChecked = s.LetterBucketFallback ?? true;

        var cap = s.MaxFilesPerFolder ?? 0;
        ChkFolderCap.IsChecked = cap > 0;
        FolderCapBox.Text = (cap > 0 ? cap : 99).ToString();
    }

    private static void SetIndex(System.Windows.Controls.ComboBox combo, int? idx, int count)
    {
        if (idx is int i && i >= 0 && i < count) combo.SelectedIndex = i;
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
            TagModeIndex = TagModeCombo.SelectedIndex,
            ArtModeIndex = ArtModeCombo.SelectedIndex,
            DupActionIndex = DupActionCombo.SelectedIndex,
            UseClean = ChkClean.IsChecked == true,
            UseMusicBrainz = ChkMusicBrainz.IsChecked == true,
            UseAcoustId = ChkAcoustId.IsChecked == true,
            UseShazam = ChkShazam.IsChecked == true,
            SkipExisting = ChkSkipExisting.IsChecked == true,
            Copy = RbCopy.IsChecked == true,
            DedupEnabled = ChkDedup.IsChecked == true,
            DedupByArtistTitle = ChkDupAT.IsChecked == true,
            DedupByMbid = ChkDupMbid.IsChecked == true,
            DedupByDuration = ChkDupDur.IsChecked == true,
            DedupByHash = ChkDupHash.IsChecked == true,
            HashEntireLibrary = ChkHashLib.IsChecked == true,
            DupRecycle = ChkDupRecycle.IsChecked == true,
            LetterBucketFallback = ChkLetterFallback.IsChecked == true,
            MaxFilesPerFolder = ParseFolderCap()
        });
    }

    private int ParseFolderCap()
    {
        if (ChkFolderCap.IsChecked != true) return 0;
        if (int.TryParse(FolderCapBox.Text, out var n) && n > 0) return n;
        return 0;
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
            Layout   = ((FolderLayoutOption)LayoutCombo.SelectedItem!).Layout,
            FileName = ((FileNameOption)FilenameCombo.SelectedItem!).Pattern,
            TagMode  = ((TagWriteOption)TagModeCombo.SelectedItem!).Mode,
            ArtMode  = ((CoverArtOption)ArtModeCombo.SelectedItem!).Mode,
            DupAction = ((DuplicateActionOption)DupActionCombo.SelectedItem!).Action,

            UseClean = ChkClean.IsChecked == true,
            UseMusicBrainz = ChkMusicBrainz.IsChecked == true,
            UseAcoustId = ChkAcoustId.IsChecked == true,
            UseShazam = ChkShazam.IsChecked == true,

            SkipExisting = ChkSkipExisting.IsChecked == true,
            Move = RbMove.IsChecked == true,

            DedupEnabled       = ChkDedup.IsChecked == true,
            DedupByArtistTitle = ChkDupAT.IsChecked == true,
            DedupByMbid        = ChkDupMbid.IsChecked == true,
            DedupByDuration    = ChkDupDur.IsChecked == true,
            DedupByHash        = ChkDupHash.IsChecked == true,
            HashEntireLibrary  = ChkHashLib.IsChecked == true,
            DupRecycle         = ChkDupRecycle.IsChecked == true,
            LetterBucketFallback = ChkLetterFallback.IsChecked == true,
            MaxFilesPerFolder  = ParseFolderCap(),

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

        ResetKpis();

        var progress = new Progress<SorterProgress>(p =>
        {
            Progress.Value = p.Percent;
            if (p.Stats != null) UpdateKpis(p.Stats);
            if (!string.IsNullOrEmpty(p.Line) && !ShouldHideLine(p.Line))
                AppendToLog(p.Line + Environment.NewLine);
        });

        try
        {
            var sorter = new Mp3Sorter(opts);
            await Task.Run(() => sorter.RunAsync(progress, _cts.Token), _cts.Token);
            Log("== Done.");
        }
        catch (OperationCanceledException) { Log("== Cancelled."); }
        catch (Exception ex)               { Log($"!! {ex.Message}"); }
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
        if (_cts == null) return; // Esc with no run in progress: no-op, no log noise.
        _cts.Cancel();
        Log("== Cancel requested...");
    }

    /// <summary>Hard ceiling on LogBox content so a 50K-track run doesn't bloat
    /// memory. When we cross the cap we drop the oldest 10% to amortise the
    /// trimming cost across many appends.</summary>
    private const int LogMaxChars  = 1_000_000;
    private const int LogTrimChars =   100_000;

    private void Log(string line)
    {
        AppendToLog(line + Environment.NewLine);
    }

    private void AppendToLog(string text)
    {
        LogBox.AppendText(text);
        if (LogBox.Text.Length > LogMaxChars)
        {
            var trim = LogBox.Text.IndexOf('\n', LogTrimChars);
            if (trim < 0) trim = LogTrimChars;
            LogBox.Text = "...[older log entries trimmed]...\n"
                          + LogBox.Text[(trim + 1)..];
        }
        LogBox.ScrollToEnd();
    }

    /// <summary>Compact mode: hide the indented per-step enrichment lines
    /// ("  id3 -> ...", "  mbrainz -> ..."), keeping only the per-file
    /// header and the final "  -> dest" outcome.</summary>
    private bool ShouldHideLine(string line)
    {
        if (ChkCompactLog.IsChecked != true) return false;
        if (line.StartsWith("  ", StringComparison.Ordinal)
            && !line.StartsWith("  -> ", StringComparison.Ordinal)
            && !line.StartsWith("  ! ", StringComparison.Ordinal))
            return true;
        return false;
    }

    private void ClearLogBtn_Click(object sender, RoutedEventArgs e) => LogBox.Clear();

    private void CopyLogBtn_Click(object sender, RoutedEventArgs e)
    {
        try { System.Windows.Clipboard.SetText(LogBox.Text); }
        catch { /* clipboard can fail when other apps hold it; non-fatal */ }
    }

    private void ResetKpis()
    {
        KpiMatchedNum.Text  = "0";
        KpiMatchedSub.Text  = "starting…";
        KpiBucketedNum.Text = "0";
        KpiBucketedSub.Text = "letter fallback or _Unsorted";
        KpiDupesNum.Text    = "0";
        KpiDupesSub.Text    = "removed";
        KpiFailedNum.Text   = "0";
        KpiFailedSub.Text   = "errors";
        StatusLeft.Text     = "starting…";
        StatusRight.Text    = "—";
    }

    private void UpdateKpis(SorterStats s)
    {
        KpiMatchedNum.Text  = s.Matched.ToString("N0");
        KpiMatchedSub.Text  = s.Total > 0 ? $"of {s.Total:N0}" : "of —";

        KpiBucketedNum.Text = s.Bucketed.ToString("N0");
        KpiBucketedSub.Text = s.Skipped > 0 ? $"+ {s.Skipped:N0} skipped" : "letter fallback or _Unsorted";

        KpiDupesNum.Text    = s.Deduped.ToString("N0");
        KpiDupesSub.Text    = s.DedupedBytes > 0 ? $"~{FormatBytes(s.DedupedBytes)} recovered" : "removed";

        KpiFailedNum.Text   = s.Failed.ToString("N0");
        KpiFailedSub.Text   = s.Failed == 0 ? "no errors" : s.Failed == 1 ? "error" : "errors";

        StatusLeft.Text  = $"{s.Done:N0} / {s.Total:N0} files";
        StatusRight.Text = s.Elapsed.TotalSeconds < 1
            ? "—"
            : $"{FormatElapsed(s.Elapsed)} · {s.FilesPerSecond:0.#} files/s";
    }

    private static string FormatBytes(long b)
    {
        if (b <= 0) return "0 B";
        string[] u = { "B", "KB", "MB", "GB", "TB" };
        double v = b; int i = 0;
        while (v >= 1024 && i < u.Length - 1) { v /= 1024; i++; }
        return $"{v:0.##} {u[i]}";
    }

    private static string FormatElapsed(TimeSpan t)
        => t.TotalHours >= 1
            ? $"{(int)t.TotalHours}:{t.Minutes:00}:{t.Seconds:00}"
            : $"{t.Minutes:00}:{t.Seconds:00}";

    // ============================== Analyze tab ==============================

    private void BrowseAnalyzeFolder_Click(object sender, RoutedEventArgs e)
        => PickFolder(AnalyzeFolderBox);

    private async void AnalyzeBtn_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(AnalyzeFolderBox.Text) || !Directory.Exists(AnalyzeFolderBox.Text))
        {
            System.Windows.MessageBox.Show("Please choose a folder to analyze.", "Music Sorter",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        AnalyzeBtn.IsEnabled = false;
        AnCancelBtn.IsEnabled = true;
        AnProgress.IsIndeterminate = true;
        AnLogBox.Clear();
        ResetAnalyzePanels();
        _anCts = new CancellationTokenSource();

        var progress = new Progress<string>(line =>
        {
            AnLogBox.AppendText(line + Environment.NewLine);
            AnLogBox.ScrollToEnd();
        });

        try
        {
            var analyzer = new LibraryAnalyzer();
            var folder = AnalyzeFolderBox.Text;
            var result = await Task.Run(() => analyzer.AnalyzeAsync(folder, progress, _anCts.Token), _anCts.Token);
            RenderAnalysis(result);
            AnLogBox.AppendText("== Analysis complete." + Environment.NewLine);
        }
        catch (OperationCanceledException)
        {
            AnLogBox.AppendText("== Cancelled." + Environment.NewLine);
        }
        catch (Exception ex)
        {
            AnLogBox.AppendText("!! " + ex.Message + Environment.NewLine);
        }
        finally
        {
            AnalyzeBtn.IsEnabled = true;
            AnCancelBtn.IsEnabled = false;
            AnProgress.IsIndeterminate = false;
            AnProgress.Value = 100;
            _anCts?.Dispose();
            _anCts = null;
        }
    }

    private void AnCancelBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_anCts == null) return;
        _anCts.Cancel();
        AnLogBox.AppendText("== Cancel requested..." + Environment.NewLine);
    }

    private void ResetAnalyzePanels()
    {
        AnFilesNum.Text = "—";    AnFilesSub.Text = "scanning…";
        AnSizeNum.Text  = "—";    AnSizeSub.Text  = "—";
        AnDurNum.Text   = "—";    AnDurSub.Text   = "—";
        AnBitrateNum.Text = "—";  AnBitrateSub.Text = "kbps";

        foreach (var (bar, val) in new (System.Windows.Controls.ProgressBar, System.Windows.Controls.TextBlock)[]
        {
            (AnTitleBar, AnTitleVal), (AnArtistBar, AnArtistVal),
            (AnAlbumBar, AnAlbumVal), (AnAABar, AnAAVal),
            (AnYearBar, AnYearVal),   (AnGenreBar, AnGenreVal),
            (AnTrackBar, AnTrackVal), (AnMbidBar, AnMbidVal),
            (AnBr1Bar, AnBr1Val),     (AnBr2Bar, AnBr2Val),
            (AnBr3Bar, AnBr3Val),     (AnBr4Bar, AnBr4Val),
            (AnBr5Bar, AnBr5Val),     (AnBr6Bar, AnBr6Val),
        })
        {
            bar.Value = 0; val.Text = "—";
        }

        AnGenresList.ItemsSource = null;
        AnArtistsList.ItemsSource = null;
        AnIssuesPanel.Children.Clear();
    }

    private void RenderAnalysis(LibraryAnalysis r)
    {
        AnFilesNum.Text  = r.TotalFiles.ToString("N0");
        AnFilesSub.Text  = r.UnreadableFiles > 0
            ? $"{r.UnreadableFiles:N0} unreadable"
            : $"scanned in {(r.ScanElapsed.TotalSeconds >= 1 ? FormatElapsed(r.ScanElapsed) : "<1s")}";

        AnSizeNum.Text   = FormatBytes(r.TotalBytes);
        AnSizeSub.Text   = r.ScannedFiles > 0
            ? $"avg {FormatBytes(r.TotalBytes / Math.Max(1, r.ScannedFiles))} / file"
            : "—";

        AnDurNum.Text    = FormatHours(r.TotalDuration);
        AnDurSub.Text    = r.ScannedFiles > 0
            ? $"avg {Math.Round(r.TotalDuration.TotalSeconds / Math.Max(1, r.ScannedFiles))}s / track"
            : "—";

        AnBitrateNum.Text = r.AvgBitrateKbps > 0 ? r.AvgBitrateKbps.ToString() : "—";
        AnBitrateSub.Text = r.AvgBitrateKbps > 0
            ? $"min {r.MinBitrateKbps} · max {r.MaxBitrateKbps} kbps"
            : "kbps";

        SetMeter(AnTitleBar,  AnTitleVal,  r.FilesWithTitle,       r.ScannedFiles);
        SetMeter(AnArtistBar, AnArtistVal, r.FilesWithArtist,      r.ScannedFiles);
        SetMeter(AnAlbumBar,  AnAlbumVal,  r.FilesWithAlbum,       r.ScannedFiles);
        SetMeter(AnAABar,     AnAAVal,     r.FilesWithAlbumArtist, r.ScannedFiles);
        SetMeter(AnYearBar,   AnYearVal,   r.FilesWithYear,        r.ScannedFiles);
        SetMeter(AnGenreBar,  AnGenreVal,  r.FilesWithGenre,       r.ScannedFiles);
        SetMeter(AnTrackBar,  AnTrackVal,  r.FilesWithTrackNumber, r.ScannedFiles);
        SetMeter(AnMbidBar,   AnMbidVal,   r.FilesWithMbid,        r.ScannedFiles);

        var br = r.BitrateBuckets;
        SetCountMeter(AnBr1Bar, AnBr1Val, br.GetValueOrDefault("<128"), r.ScannedFiles);
        SetCountMeter(AnBr2Bar, AnBr2Val, br.GetValueOrDefault("128"),  r.ScannedFiles);
        SetCountMeter(AnBr3Bar, AnBr3Val, br.GetValueOrDefault("192"),  r.ScannedFiles);
        SetCountMeter(AnBr4Bar, AnBr4Val, br.GetValueOrDefault("256"),  r.ScannedFiles);
        SetCountMeter(AnBr5Bar, AnBr5Val, br.GetValueOrDefault("320"),  r.ScannedFiles);
        SetCountMeter(AnBr6Bar, AnBr6Val, br.GetValueOrDefault(">320"), r.ScannedFiles);

        AnGenresList.ItemsSource  = r.TopGenres .Select(kv => $"{kv.Value,5:N0}   {kv.Key}").ToArray();
        AnArtistsList.ItemsSource = r.TopArtists.Select(kv => $"{kv.Value,5:N0}   {kv.Key}").ToArray();

        AnIssuesPanel.Children.Clear();
        AddIssue(r.SuspectedYouTubeRips,         "likely YouTube rips (filename has '(Official...)', trailing 11-char id, ...)");
        AddIssue(r.SuspectedDuplicateGroups,     $"duplicate group{(r.SuspectedDuplicateGroups == 1 ? "" : "s")} ({r.SuspectedDuplicateFiles} files, ~{FormatBytes(r.DuplicateRecoverableBytes)} reclaimable)");
        AddIssue(r.FilesWithoutAnyTags,          "files with no usable tags (the cleaned filename is the only clue)");
        AddIssue(r.FilesWithoutArtistOrTitle,    "files missing artist OR title");
        if (AnIssuesPanel.Children.Count == 0)
        {
            AnIssuesPanel.Children.Add(new System.Windows.Controls.TextBlock
            {
                Text = "No issues detected — your library looks clean.",
                Foreground = (System.Windows.Media.Brush)FindResource("SuccessBrush"),
                FontSize = 12
            });
        }
    }

    private void AddIssue(int count, string label)
    {
        if (count <= 0) return;
        var line = new System.Windows.Controls.TextBlock
        {
            Margin = new Thickness(0, 2, 0, 2),
            FontSize = 12,
            Inlines =
            {
                new System.Windows.Documents.Run
                {
                    Text = "⚠ ",
                    Foreground = (System.Windows.Media.Brush)FindResource("WarnBrush"),
                    FontWeight = FontWeights.Bold
                },
                new System.Windows.Documents.Run
                {
                    Text = $"{count:N0} ",
                    Foreground = (System.Windows.Media.Brush)FindResource("TextBrush"),
                    FontWeight = FontWeights.SemiBold
                },
                new System.Windows.Documents.Run
                {
                    Text = label,
                    Foreground = (System.Windows.Media.Brush)FindResource("MutedBrush")
                }
            }
        };
        AnIssuesPanel.Children.Add(line);
    }

    private static void SetMeter(System.Windows.Controls.ProgressBar bar,
                                 System.Windows.Controls.TextBlock val,
                                 int n, int total)
    {
        var pct = total > 0 ? 100.0 * n / total : 0;
        bar.Maximum = 100;
        bar.Value   = pct;
        val.Text    = total > 0 ? $"{pct:0}% ({n:N0})" : "—";
    }
    private static void SetCountMeter(System.Windows.Controls.ProgressBar bar,
                                      System.Windows.Controls.TextBlock val,
                                      int n, int total)
    {
        var pct = total > 0 ? 100.0 * n / total : 0;
        bar.Maximum = 100;
        bar.Value   = pct;
        val.Text    = total > 0 ? $"{pct:0}% ({n:N0})" : "—";
    }

    private static string FormatHours(TimeSpan t)
    {
        var h = (int)t.TotalHours;
        return h >= 1 ? $"{h}h {t.Minutes:00}m" : $"{t.Minutes:00}m {t.Seconds:00}s";
    }
}
