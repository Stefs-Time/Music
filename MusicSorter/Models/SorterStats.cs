namespace MusicSorter.Models;

/// <summary>
/// Live statistics emitted by <c>Mp3Sorter</c> after every processed file. The UI
/// reads this from <see cref="SorterProgress.Stats"/> and pushes the values into
/// the KPI cards.
/// </summary>
public sealed class SorterStats
{
    public int Total    { get; set; }      // files queued for processing
    public int Done     { get; set; }      // processed (any outcome)
    public int Matched  { get; set; }      // confidence >= 0.55, placed in real layout
    public int Bucketed { get; set; }      // letter-bucket fallback or _Unsorted
    public int Skipped  { get; set; }      // dest already exists / already at dest
    public int Deduped  { get; set; }      // duplicate removed (incoming or library)
    public int Failed   { get; set; }      // exception during processing

    public TimeSpan Elapsed { get; set; }
    public long DedupedBytes { get; set; }  // size of deleted dup files (informational)

    /// <summary>How many confident matches came from each enrichment source (id3 /
    /// filename / musicbrainz / acoustid / shazam). Useful for the post-run summary.</summary>
    public Dictionary<string, int> MatchedBySource { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);

    public double FilesPerSecond =>
        Elapsed.TotalSeconds > 0.5 ? Done / Elapsed.TotalSeconds : 0;

    /// <summary>Independent copy so the UI thread never observes a moving target.</summary>
    public SorterStats Snapshot() => new()
    {
        Total = Total, Done = Done, Matched = Matched, Bucketed = Bucketed,
        Skipped = Skipped, Deduped = Deduped, Failed = Failed,
        Elapsed = Elapsed, DedupedBytes = DedupedBytes,
        MatchedBySource = new Dictionary<string, int>(MatchedBySource, StringComparer.OrdinalIgnoreCase)
    };
}
