namespace MusicSorter.Models;

public sealed class TrackMetadata
{
    public string? Title { get; set; }
    public string? Artist { get; set; }
    public string? AlbumArtist { get; set; }
    public string? Album { get; set; }
    public uint Year { get; set; }
    public uint TrackNumber { get; set; }
    public uint TrackCount { get; set; }
    public uint Disc { get; set; }
    public string? Genre { get; set; }
    public string? MusicBrainzReleaseId { get; set; }
    public string? MusicBrainzReleaseGroupId { get; set; }
    public string? MusicBrainzRecordingId { get; set; }

    /// <summary>Direct URL to a cover-art image (e.g. from Shazam). Used as a fallback
    /// when Cover Art Archive doesn't have one for the MBID.</summary>
    public string? AlbumArtUrl { get; set; }

    /// <summary>0..1 confidence the metadata is correct.</summary>
    public double Confidence { get; set; }

    /// <summary>Which source produced this metadata.</summary>
    public string Source { get; set; } = "none";

    public bool HasArtistAndTitle =>
        !string.IsNullOrWhiteSpace(Artist) && !string.IsNullOrWhiteSpace(Title);

    public TrackMetadata Clone() => (TrackMetadata)MemberwiseClone();

    public override string ToString()
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(Artist)) parts.Add(Artist!);
        if (!string.IsNullOrWhiteSpace(Title)) parts.Add(Title!);
        if (!string.IsNullOrWhiteSpace(Album)) parts.Add($"({Album})");
        return string.Join(" - ", parts);
    }
}
