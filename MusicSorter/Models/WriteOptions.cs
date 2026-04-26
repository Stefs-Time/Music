namespace MusicSorter.Models;

/// <summary>How aggressively to write enriched metadata into the destination MP3.</summary>
public enum TagWriteMode
{
    /// <summary>Write every enriched field, overwriting whatever was there.</summary>
    Full,
    /// <summary>Only fill empty fields; never overwrite existing values.</summary>
    FillBlanks,
    /// <summary>Write only Title + Artist; clear the rest.</summary>
    Minimal,
    /// <summary>Remove every ID3 tag — pure audio (rely on filename only).</summary>
    Strip
}

/// <summary>What to do with cover art on the final MP3.</summary>
public enum CoverArtMode
{
    /// <summary>Download cover art and embed it (Cover Art Archive / Shazam).</summary>
    EmbedDownloaded,
    /// <summary>Don't change embedded art — keep whatever's already in the file.</summary>
    KeepExisting,
    /// <summary>Strip every embedded picture — audio-only output.</summary>
    Strip
}

/// <summary>What to do when a song that already exists in the library shows up again.</summary>
public enum DuplicateAction
{
    /// <summary>Compare bitrate (size, duration as tiebreakers); keep the better one.</summary>
    KeepBest,
    /// <summary>No automatic action — just log the dup.</summary>
    LogOnly
}

public sealed record TagWriteOption(TagWriteMode Mode, string Label);
public sealed record CoverArtOption(CoverArtMode Mode, string Label);
public sealed record DuplicateActionOption(DuplicateAction Action, string Label);

public static class WriteOptions
{
    public static readonly IReadOnlyList<TagWriteOption> TagModes = new[]
    {
        new TagWriteOption(TagWriteMode.Full,       "Full enrichment (overwrite existing tags)"),
        new TagWriteOption(TagWriteMode.FillBlanks, "Fill blanks only (don't overwrite)"),
        new TagWriteOption(TagWriteMode.Minimal,    "Minimal (Title + Artist only, clear rest)"),
        new TagWriteOption(TagWriteMode.Strip,      "Strip all tags (audio-only, filename only)")
    };

    public static readonly IReadOnlyList<CoverArtOption> ArtModes = new[]
    {
        new CoverArtOption(CoverArtMode.EmbedDownloaded, "Download + embed cover art"),
        new CoverArtOption(CoverArtMode.KeepExisting,    "Keep existing art, don't download"),
        new CoverArtOption(CoverArtMode.Strip,           "Strip all art (audio-only)")
    };

    public static readonly IReadOnlyList<DuplicateActionOption> DupActions = new[]
    {
        new DuplicateActionOption(DuplicateAction.KeepBest, "Keep highest-quality, remove others"),
        new DuplicateActionOption(DuplicateAction.LogOnly,  "Log only (rename to '(2).mp3')")
    };
}
