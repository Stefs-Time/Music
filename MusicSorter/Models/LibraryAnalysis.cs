namespace MusicSorter.Models;

/// <summary>
/// Read-only snapshot of an MP3 library. Produced by <c>LibraryAnalyzer</c>.
/// No file modifications happen during analysis.
/// </summary>
public sealed class LibraryAnalysis
{
    public string Folder { get; set; } = "";

    // Volume
    public int TotalFiles      { get; set; }
    public int UnreadableFiles { get; set; }
    public long TotalBytes     { get; set; }
    public TimeSpan TotalDuration { get; set; }
    public TimeSpan ScanElapsed   { get; set; }

    // Bitrate
    public int AvgBitrateKbps { get; set; }
    public int MinBitrateKbps { get; set; }
    public int MaxBitrateKbps { get; set; }
    /// <summary>Buckets: "&lt;128", "128", "192", "256", "320", "&gt;320".</summary>
    public Dictionary<string, int> BitrateBuckets { get; set; } = new();

    // Tag completeness — each is a count out of TotalFiles - UnreadableFiles.
    public int FilesWithTitle       { get; set; }
    public int FilesWithArtist      { get; set; }
    public int FilesWithAlbum       { get; set; }
    public int FilesWithAlbumArtist { get; set; }
    public int FilesWithYear        { get; set; }
    public int FilesWithGenre       { get; set; }
    public int FilesWithTrackNumber { get; set; }
    public int FilesWithMbid        { get; set; }

    // Distributions
    public List<KeyValuePair<string, int>> TopGenres  { get; set; } = new();
    public List<KeyValuePair<string, int>> TopArtists { get; set; } = new();
    public List<KeyValuePair<string, int>> TopAlbums  { get; set; } = new();
    /// <summary>Year -> count, for the years that have at least one tagged file.</summary>
    public Dictionary<int, int> YearHistogram { get; set; } = new();

    // Issue findings — counts only; the UI shows them with file-count summaries.
    public int SuspectedYouTubeRips      { get; set; }
    public int SuspectedDuplicateGroups  { get; set; }   // groups of 2+ files
    public int SuspectedDuplicateFiles   { get; set; }   // total files involved
    public int FilesWithoutAnyTags       { get; set; }
    public int FilesWithoutArtistOrTitle { get; set; }

    // Bytes that could be reclaimed by removing all-but-one of each suspected dup group.
    public long DuplicateRecoverableBytes { get; set; }

    // Sample paths so the user can see a few examples without scrolling thousands.
    public List<string> SampleYouTubeRips { get; set; } = new();
    public List<string> SampleUntagged    { get; set; } = new();
    public List<string> SampleDuplicates  { get; set; } = new();

    public int ScannedFiles => TotalFiles - UnreadableFiles;
    public double Percent(int n) => ScannedFiles > 0 ? 100.0 * n / ScannedFiles : 0;
}
