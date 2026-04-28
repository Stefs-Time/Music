using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using MusicSorter.Models;

namespace MusicSorter.Services;

/// <summary>
/// Read-only walk of an MP3 library that produces a <see cref="LibraryAnalysis"/>.
/// Reuses AudioFile.Read for the single-pass tag/property read; never writes.
/// </summary>
public sealed class LibraryAnalyzer
{
    private static readonly Regex YouTubeIdSuffix = new(
        @"[\s_\-][\(\[]?[A-Za-z0-9_-]{11}[\)\]]?\s*$", RegexOptions.Compiled);
    private static readonly Regex YouTubeBracketCruft = new(
        @"[\(\[][^()\[\]]*\b(?:official\s*(?:music\s*)?video|lyric\s*video|hd|4k|1080p|720p|audio|mv|vevo)\b[^()\[\]]*[\)\]]",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public async Task<LibraryAnalysis> AnalyzeAsync(string folder, IProgress<string>? progress, CancellationToken ct)
    {
        var result = new LibraryAnalysis { Folder = folder };
        if (!Directory.Exists(folder)) return result;

        var sw = Stopwatch.StartNew();
        var files = Directory.EnumerateFiles(folder, "*.mp3", SearchOption.AllDirectories).ToList();
        result.TotalFiles = files.Count;
        progress?.Report($"== Scanning '{folder}' ... found {files.Count} mp3 file(s).");
        if (files.Count == 0) { result.ScanElapsed = sw.Elapsed; return result; }

        var bitrates = new List<int>();
        var artists  = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var albums   = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var genres   = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var dupGroups = new Dictionary<string, List<(string Path, long Bytes)>>(StringComparer.Ordinal);
        var bitrateBuckets = new Dictionary<string, int>
        {
            ["<128"]=0, ["128"]=0, ["192"]=0, ["256"]=0, ["320"]=0, [">320"]=0
        };

        await Task.Run(() =>
        {
            int n = 0;
            foreach (var file in files)
            {
                ct.ThrowIfCancellationRequested();
                n++;
                if (n % 250 == 0)
                    progress?.Report($"   scanned {n:N0} / {files.Count:N0}");

                var info = AudioFile.Read(file);
                if (info == null) { result.UnreadableFiles++; continue; }

                result.TotalBytes    += info.FileSize;
                result.TotalDuration += info.Duration;
                if (info.Bitrate > 0)
                {
                    bitrates.Add(info.Bitrate);
                    bitrateBuckets[BitrateBucket(info.Bitrate)]++;
                }

                var t = info.Tags;
                bool hasTitle  = !string.IsNullOrWhiteSpace(t?.Title);
                bool hasArtist = !string.IsNullOrWhiteSpace(t?.Artist);
                bool hasAlbum  = !string.IsNullOrWhiteSpace(t?.Album);
                bool hasYear   = (t?.Year ?? 0) > 0;
                bool hasGenre  = !string.IsNullOrWhiteSpace(t?.Genre);
                bool hasTrack  = (t?.TrackNumber ?? 0) > 0;
                bool hasMbid   = !string.IsNullOrWhiteSpace(t?.MusicBrainzRecordingId);
                bool hasAA     = !string.IsNullOrWhiteSpace(t?.AlbumArtist);

                if (hasTitle)  result.FilesWithTitle++;
                if (hasArtist) result.FilesWithArtist++;
                if (hasAlbum)  result.FilesWithAlbum++;
                if (hasAA)     result.FilesWithAlbumArtist++;
                if (hasYear)   result.FilesWithYear++;
                if (hasGenre)  result.FilesWithGenre++;
                if (hasTrack)  result.FilesWithTrackNumber++;
                if (hasMbid)   result.FilesWithMbid++;

                if (hasArtist) Inc(artists, t!.Artist!.Trim());
                if (hasAlbum && hasArtist) Inc(albums, $"{t!.Artist!.Trim()} — {t.Album!.Trim()}");
                if (hasGenre)  Inc(genres, NormalizeGenre(t!.Genre!));
                if (hasYear)   IncYear(result.YearHistogram, (int)t!.Year);

                if (t == null || (!hasTitle && !hasArtist))
                {
                    result.FilesWithoutAnyTags++;
                    if (result.SampleUntagged.Count < 25) result.SampleUntagged.Add(file);
                }
                else if (!hasArtist || !hasTitle)
                {
                    result.FilesWithoutArtistOrTitle++;
                }

                if (LooksLikeYouTubeRip(file))
                {
                    result.SuspectedYouTubeRips++;
                    if (result.SampleYouTubeRips.Count < 25) result.SampleYouTubeRips.Add(file);
                }

                if (hasArtist && hasTitle)
                {
                    var key = "AT|" + Norm(t!.Artist!) + "|" + Norm(t.Title!);
                    if (!dupGroups.TryGetValue(key, out var list))
                    { list = new List<(string, long)>(); dupGroups[key] = list; }
                    list.Add((file, info.FileSize));
                }
            }
        }, ct);

        // Bitrate aggregates
        if (bitrates.Count > 0)
        {
            result.AvgBitrateKbps = (int)bitrates.Average();
            result.MinBitrateKbps = bitrates.Min();
            result.MaxBitrateKbps = bitrates.Max();
        }
        result.BitrateBuckets = bitrateBuckets;

        // Top-N
        result.TopArtists = artists.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key).Take(15).ToList();
        result.TopAlbums  = albums .OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key).Take(15).ToList();
        result.TopGenres  = genres .OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key).Take(15).ToList();

        // Duplicate groups
        foreach (var (key, list) in dupGroups)
        {
            if (list.Count < 2) continue;
            result.SuspectedDuplicateGroups++;
            result.SuspectedDuplicateFiles += list.Count;
            // Reclaimable = sum of all but the largest (assume that's the keeper).
            var bytes = list.OrderByDescending(x => x.Bytes).Skip(1).Sum(x => x.Bytes);
            result.DuplicateRecoverableBytes += bytes;
            if (result.SampleDuplicates.Count < 50)
            {
                foreach (var (p, _) in list.Take(2))
                {
                    if (result.SampleDuplicates.Count >= 50) break;
                    result.SampleDuplicates.Add(p);
                }
            }
        }

        result.ScanElapsed = sw.Elapsed;
        progress?.Report($"== Done in {Format(result.ScanElapsed)}: " +
                         $"{result.ScannedFiles:N0} readable, {result.UnreadableFiles:N0} unreadable.");
        return result;
    }

    /// <summary>Heuristic: filename has bracketed YouTube cruft, or ends with the
    /// classic 11-char YouTube video id suffix.</summary>
    public static bool LooksLikeYouTubeRip(string path)
    {
        var name = Path.GetFileNameWithoutExtension(path) ?? "";
        if (string.IsNullOrEmpty(name)) return false;
        if (YouTubeBracketCruft.IsMatch(name)) return true;
        if (YouTubeIdSuffix.IsMatch(name))     return true;
        return false;
    }

    private static string BitrateBucket(int kbps)
    {
        if (kbps < 128) return "<128";
        if (kbps < 192) return "128";
        if (kbps < 256) return "192";
        if (kbps < 320) return "256";
        if (kbps == 320) return "320";
        return ">320";
    }

    private static void Inc(Dictionary<string, int> d, string key)
    {
        d.TryGetValue(key, out var n);
        d[key] = n + 1;
    }
    private static void IncYear(Dictionary<int, int> d, int year)
    {
        d.TryGetValue(year, out var n);
        d[year] = n + 1;
    }

    /// <summary>Loose normalisation for the duplicate key — same approach as
    /// DuplicateIndex but inlined so we don't pull in SortOptions for a read-only
    /// scan.</summary>
    private static string Norm(string s)
    {
        var sb = new System.Text.StringBuilder(s.Length);
        foreach (var ch in s.ToLowerInvariant())
            if (char.IsLetterOrDigit(ch)) sb.Append(ch);
        return sb.ToString();
    }

    /// <summary>Group "Rock", "rock", " ROCK " into one bucket. Title-case the result
    /// for the display.</summary>
    private static string NormalizeGenre(string g)
    {
        var trimmed = g.Trim();
        if (trimmed.Length == 0) return g;
        return char.ToUpperInvariant(trimmed[0]) + trimmed[1..].ToLowerInvariant();
    }

    private static string Format(TimeSpan t)
        => t.TotalHours >= 1
            ? $"{(int)t.TotalHours}:{t.Minutes:00}:{t.Seconds:00}"
            : $"{t.Minutes:00}:{t.Seconds:00}";
}
