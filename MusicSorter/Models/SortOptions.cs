namespace MusicSorter.Models;

public sealed class SortOptions
{
    public string Source { get; set; } = "";
    public string Output { get; set; } = "";
    public FolderLayout Layout { get; set; } = FolderLayout.ArtistAlbumTrack;
    public FileNamePattern FileName { get; set; } = FileNamePattern.TrackTitle;

    public bool UseClean { get; set; } = true;
    public bool UseMusicBrainz { get; set; } = true;
    public bool UseAcoustId { get; set; } = true;
    public bool UseShazam { get; set; } = true;
    public bool UseCoverArt { get; set; } = true;

    public bool OverwriteTags { get; set; } = true;
    public bool SkipExisting { get; set; } = true;
    public bool Move { get; set; } = true;

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
    public bool? UseClean { get; set; }
    public bool? UseMusicBrainz { get; set; }
    public bool? UseAcoustId { get; set; }
    public bool? UseShazam { get; set; }
    public bool? UseCoverArt { get; set; }
    public bool? OverwriteTags { get; set; }
    public bool? SkipExisting { get; set; }
    public bool? Copy { get; set; }
}
