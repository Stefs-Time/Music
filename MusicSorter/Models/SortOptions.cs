namespace MusicSorter.Models;

public sealed class SortOptions
{
    public string Source { get; set; } = "";
    public string Output { get; set; } = "";
    public FolderLayout Layout { get; set; } = FolderLayout.ArtistAlbumTrack;
    public FileNamePattern FileName { get; set; } = FileNamePattern.TrackTitle;

    // Enrichment cascade
    public bool UseClean { get; set; } = true;
    public bool UseMusicBrainz { get; set; } = true;
    public bool UseAcoustId { get; set; } = true;
    public bool UseShazam { get; set; } = true;

    // How tags + art get written to the final file
    public TagWriteMode TagMode { get; set; } = TagWriteMode.Full;
    public CoverArtMode ArtMode { get; set; } = CoverArtMode.EmbedDownloaded;

    // File operation
    public bool SkipExisting { get; set; } = true;
    public bool Move { get; set; } = true;

    /// <summary>0 = unlimited. When &gt; 0, a leaf folder that already holds this many
    /// .mp3 files is "full" and the next file is redirected to "Folder (2)", "(3)", etc.</summary>
    public int MaxFilesPerFolder { get; set; } = 0;

    // Duplicate detection
    public bool DedupEnabled { get; set; } = true;
    public bool DedupByArtistTitle { get; set; } = true;
    public bool DedupByMbid { get; set; } = true;
    public bool DedupByDuration { get; set; } = true;
    public bool DedupByHash { get; set; } = true;
    /// <summary>Hash every existing library file at startup (slow). Off by default — only
    /// hashes new incoming files + files that match on metadata.</summary>
    public bool HashEntireLibrary { get; set; } = false;
    public DuplicateAction DupAction { get; set; } = DuplicateAction.KeepBest;
    /// <summary>Send removed duplicates to Recycle Bin instead of permanently deleting.</summary>
    public bool DupRecycle { get; set; } = true;

    // Credentials
    public string AcoustIdKey { get; set; } = "";
    public string ShazamKey { get; set; } = "";
    public string ShazamHost { get; set; } = "shazam.p.rapidapi.com";
    public string FpcalcPath { get; set; } = "";
}

public sealed class SorterProgress
{
    public int Percent { get; set; }
    public string Line { get; set; } = "";
}

public sealed class AppSettings
{
    public string? Source { get; set; }
    public string? Output { get; set; }
    public string? AcoustIdKey { get; set; }
    public string? ShazamKey { get; set; }
    public string? FpcalcPath { get; set; }
    public string? ShazamHost { get; set; }
    public int? LayoutIndex { get; set; }
    public int? FileNameIndex { get; set; }
    public int? TagModeIndex { get; set; }
    public int? ArtModeIndex { get; set; }
    public int? DupActionIndex { get; set; }
    public int? MaxFilesPerFolder { get; set; }
    public bool? UseClean { get; set; }
    public bool? UseMusicBrainz { get; set; }
    public bool? UseAcoustId { get; set; }
    public bool? UseShazam { get; set; }
    public bool? SkipExisting { get; set; }
    public bool? Copy { get; set; }
    public bool? DedupEnabled { get; set; }
    public bool? DedupByArtistTitle { get; set; }
    public bool? DedupByMbid { get; set; }
    public bool? DedupByDuration { get; set; }
    public bool? DedupByHash { get; set; }
    public bool? HashEntireLibrary { get; set; }
    public bool? DupRecycle { get; set; }
}
