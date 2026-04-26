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
        var files = Directory.EnumerateFiles(_opts.Source, "*.mp3", SearchOption.AllDirectories).ToList();
        Report(progress, 0, $"== Scanning '{_opts.Source}' ... found {files.Count} mp3 file(s).");
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

                if (_opts.SkipExisting && File.Exists(dest) && AreSameFile(file, dest))
                {
                    Report(progress, pct, "  -> already at destination, skipped");
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
