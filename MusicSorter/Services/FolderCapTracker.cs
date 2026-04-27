using System.IO;

namespace MusicSorter.Services;

/// <summary>
/// Caps the number of files in any leaf folder. When the natural target folder is
/// already at the cap, redirect the file to "Folder (2)", "Folder (3)", and so on.
///
/// Counts are seeded from disk on first lookup (so we honour pre-existing files in a
/// previously-organized library) and then maintained in-memory so we don't re-enumerate
/// the directory on every call.
/// </summary>
public sealed class FolderCapTracker
{
    private readonly int _max;
    private readonly Dictionary<string, int> _counts = new(StringComparer.OrdinalIgnoreCase);

    public FolderCapTracker(int max) { _max = max; }

    public bool Enabled => _max > 0;

    /// <summary>
    /// Compute the actual destination for <paramref name="naturalDest"/>, possibly
    /// redirecting to a numbered sibling folder if the natural one is full.  Does NOT
    /// reserve a slot — call <see cref="Confirm"/> after a successful placement.
    /// </summary>
    public string Resolve(string naturalDest)
    {
        if (!Enabled) return naturalDest;

        var dir = Path.GetDirectoryName(naturalDest);
        if (string.IsNullOrEmpty(dir)) return naturalDest;
        var fname = Path.GetFileName(naturalDest);

        string baseDir = dir;
        int idx = 1;
        string current = baseDir;
        while (idx <= 1000)
        {
            if (GetOrSeed(current) < _max)
                return Path.Combine(current, fname);
            idx++;
            current = $"{baseDir} ({idx})";
        }
        // safety fallback — give up after 1000 siblings
        return Path.Combine(current, fname);
    }

    /// <summary>
    /// Record a successful placement at <paramref name="actualDest"/>. Must be called
    /// after the file has actually arrived on disk so counts stay accurate.
    /// </summary>
    public void Confirm(string actualDest)
    {
        if (!Enabled) return;
        var dir = Path.GetDirectoryName(actualDest);
        if (string.IsNullOrEmpty(dir)) return;
        _counts[dir] = GetOrSeed(dir) + 1;
    }

    private int GetOrSeed(string dir)
    {
        if (_counts.TryGetValue(dir, out var n)) return n;
        n = Directory.Exists(dir) ? CountMp3s(dir) : 0;
        _counts[dir] = n;
        return n;
    }

    private static int CountMp3s(string dir)
    {
        try { return Directory.EnumerateFiles(dir, "*.mp3").Count(); }
        catch { return int.MaxValue; }
    }
}
