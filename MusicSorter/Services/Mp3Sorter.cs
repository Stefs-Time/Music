using System.IO;
using MusicSorter.Models;

namespace MusicSorter.Services;

/// <summary>
/// Top-level orchestrator: scans the source folder, enriches every mp3, then
/// renames + moves/copies it into the destination layout.
/// </summary>
public sealed class Mp3Sorter
{
    private readonly SortOptions _opts;
    private readonly EnrichmentPipeline _pipeline;

    public Mp3Sorter(SortOptions opts)
    {
        _opts = opts;
        _pipeline = new EnrichmentPipeline(opts);
    }

    public async Task RunAsync(IProgress<SorterProgress> progress, CancellationToken ct)
    {
        var outputFull = Path.GetFullPath(_opts.Output);
        var sourceFull = Path.GetFullPath(_opts.Source);
        var sameRoot   = string.Equals(outputFull.TrimEnd(Path.DirectorySeparatorChar),
                                       sourceFull.TrimEnd(Path.DirectorySeparatorChar),
                                       StringComparison.OrdinalIgnoreCase);

        var allFiles = Directory.EnumerateFiles(_opts.Source, "*.mp3", SearchOption.AllDirectories).ToList();

        // When source ≠ output, drop anything that already lives under the output root
        // (e.g. user re-ran into the same library). When source == output the user is
        // explicitly re-organizing in place — keep everything and rely on the per-file
        // same-path / skip-existing checks.
        var unsortedRoot = Path.Combine(outputFull, "_Unsorted");
        var files = (sameRoot ? allFiles : allFiles.Where(p => !IsUnder(p, outputFull)))
                    .Where(p => !IsUnder(p, unsortedRoot))
                    .ToList();
        var preFiltered = allFiles.Count - files.Count;

        Report(progress, 0, $"== Scanning '{_opts.Source}' ... found {allFiles.Count} mp3 file(s).");
        if (preFiltered > 0)
            Report(progress, 0, $"   ({preFiltered} skipped: under output root or _Unsorted)");
        Report(progress, 0, $"   Layout    : {_opts.Layout}");
        Report(progress, 0, $"   File name : {_opts.FileName}");
        Report(progress, 0, $"   Mode      : {(_opts.Move ? "MOVE" : "COPY")}");
        Report(progress, 0, $"   Sources   : clean={_opts.UseClean}, mb={_opts.UseMusicBrainz}, " +
                            $"acoustid={_opts.UseAcoustId}, shazam={_opts.UseShazam}, art={_opts.UseCoverArt}");
        Report(progress, 0, "");

        Directory.CreateDirectory(_opts.Output);
        Directory.CreateDirectory(Path.Combine(_opts.Output, "_Unsorted"));

        int done = 0, matched = 0, unsorted = 0, skipped = 0, failed = 0;

        foreach (var file in files)
        {
            ct.ThrowIfCancellationRequested();
            done++;
            int pct = (int)(done * 100.0 / Math.Max(1, files.Count));
            Report(progress, pct, $"[{done}/{files.Count}] {Path.GetFileName(file)}");

            try
            {
                var (meta, cover, log) = await _pipeline.EnrichAsync(file, ct);
                foreach (var l in log) Report(progress, pct, l);

                bool confident = meta.HasArtistAndTitle && meta.Confidence >= 0.55;

                string dest;
                if (confident)
                {
                    dest = PathBuilder.BuildDestination(_opts.Output, _opts.Layout, _opts.FileName, meta,
                        Path.GetExtension(file));
                }
                else
                {
                    dest = PathBuilder.BuildUnsortedDestination(_opts.Output, file);
                }

                // Same path? Move would fail; copy would be a no-op. Always skip.
                if (AreSameFile(file, dest))
                {
                    Report(progress, pct, "  -> already at destination, skipped");
                    if (confident)
                        Mp3TagService.WriteTags(dest, meta, cover, _opts.OverwriteTags);
                    skipped++;
                    continue;
                }

                // SkipExisting: bail when a file with this name already exists at dest.
                if (_opts.SkipExisting && File.Exists(dest))
                {
                    Report(progress, pct, $"  -> destination exists, skipped: {Relative(_opts.Output, dest)}");
                    skipped++;
                    continue;
                }

                dest = PathBuilder.EnsureUnique(dest);
                Directory.CreateDirectory(Path.GetDirectoryName(dest)!);

                // Place the file first, then write tags on the destination so the
                // source folder doesn't get touched.
                if (_opts.Move) File.Move(file, dest, overwrite: false);
                else            File.Copy(file, dest, overwrite: false);

                if (confident)
                    Mp3TagService.WriteTags(dest, meta, cover, _opts.OverwriteTags);

                Report(progress, pct, confident
                    ? $"  -> {Relative(_opts.Output, dest)}    [{meta.Source}, conf {meta.Confidence:0.00}]"
                    : $"  -> _Unsorted\\{Path.GetFileName(dest)}");

                if (confident) matched++; else unsorted++;
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                failed++;
                Report(progress, pct, $"  ! {ex.Message}");
            }
        }

        Report(progress, 100, "");
        Report(progress, 100, $"== {matched} matched, {unsorted} unsorted, {skipped} skipped, {failed} failed.");

        if (_opts.Move) PruneEmptyDirs(_opts.Source);
    }

    private static bool IsUnder(string filePath, string folderFullPath)
    {
        try
        {
            var f = Path.GetFullPath(filePath);
            var root = folderFullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                       + Path.DirectorySeparatorChar;
            return f.StartsWith(root, StringComparison.OrdinalIgnoreCase);
        }
        catch { return false; }
    }

    private static bool AreSameFile(string a, string b)
    {
        try
        {
            return Path.GetFullPath(a).Equals(Path.GetFullPath(b), StringComparison.OrdinalIgnoreCase);
        }
        catch { return false; }
    }

    private static string Relative(string root, string full)
    {
        try { return Path.GetRelativePath(root, full); }
        catch { return full; }
    }

    private static void PruneEmptyDirs(string root)
    {
        if (!Directory.Exists(root)) return;
        try
        {
            foreach (var dir in Directory.EnumerateDirectories(root, "*", SearchOption.AllDirectories)
                                          .OrderByDescending(p => p.Length))
            {
                try
                {
                    if (!Directory.EnumerateFileSystemEntries(dir).Any())
                        Directory.Delete(dir);
                }
                catch { }
            }
        }
        catch { }
    }

    private static void Report(IProgress<SorterProgress> p, int pct, string line)
        => p.Report(new SorterProgress { Percent = pct, Line = line });
}
