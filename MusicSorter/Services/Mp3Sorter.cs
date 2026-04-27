using System.IO;
using MusicSorter.Models;

namespace MusicSorter.Services;

/// <summary>
/// Top-level orchestrator: scans the source folder, enriches every mp3, dedupes against
/// the destination library, then renames + moves/copies it into the destination layout.
/// </summary>
public sealed class Mp3Sorter
{
    private readonly SortOptions _opts;
    private readonly EnrichmentPipeline _pipeline;
    private readonly DuplicateIndex _index;
    private readonly FolderCapTracker _folderCap;

    public Mp3Sorter(SortOptions opts)
    {
        _opts = opts;
        _pipeline = new EnrichmentPipeline(opts);
        _index = new DuplicateIndex(opts);
        _folderCap = new FolderCapTracker(opts.MaxFilesPerFolder);
    }

    public async Task RunAsync(IProgress<SorterProgress> progress, CancellationToken ct)
    {
        var outputFull = Path.GetFullPath(_opts.Output);
        var sourceFull = Path.GetFullPath(_opts.Source);
        var sameRoot   = string.Equals(outputFull.TrimEnd(Path.DirectorySeparatorChar),
                                       sourceFull.TrimEnd(Path.DirectorySeparatorChar),
                                       StringComparison.OrdinalIgnoreCase);

        var allFiles = Directory.EnumerateFiles(_opts.Source, "*.mp3", SearchOption.AllDirectories).ToList();

        var unsortedRoot = Path.Combine(outputFull, "_Unsorted");
        var files = (sameRoot ? allFiles : allFiles.Where(p => !IsUnder(p, outputFull)))
                    .Where(p => !IsUnder(p, unsortedRoot))
                    .ToList();
        var preFiltered = allFiles.Count - files.Count;

        Report(progress, 0, $"== Scanning '{_opts.Source}' ... found {allFiles.Count} mp3 file(s).");
        if (preFiltered > 0)
            Report(progress, 0, $"   ({preFiltered} skipped: already under output root or _Unsorted)");
        Report(progress, 0, $"   Layout    : {_opts.Layout}");
        Report(progress, 0, $"   File name : {_opts.FileName}");
        Report(progress, 0, $"   Mode      : {(_opts.Move ? "MOVE" : "COPY")}");
        Report(progress, 0, $"   Tags      : {_opts.TagMode}");
        Report(progress, 0, $"   Art       : {_opts.ArtMode}");
        if (_opts.MaxFilesPerFolder > 0)
            Report(progress, 0, $"   FolderCap : {_opts.MaxFilesPerFolder} files / leaf folder");
        Report(progress, 0, $"   Sources   : clean={_opts.UseClean}, mb={_opts.UseMusicBrainz}, " +
                            $"acoustid={_opts.UseAcoustId}, shazam={_opts.UseShazam}");
        if (_opts.DedupEnabled)
            Report(progress, 0, $"   Dedup     : action={_opts.DupAction}, recycle={_opts.DupRecycle}, " +
                                $"keys=[{(_opts.DedupByArtistTitle?"AT ":"")}{(_opts.DedupByMbid?"MBID ":"")}" +
                                $"{(_opts.DedupByDuration?"AD ":"")}{(_opts.DedupByHash?"HASH":"")}]");
        Report(progress, 0, "");

        Directory.CreateDirectory(_opts.Output);
        if (!_opts.LetterBucketFallback)
            Directory.CreateDirectory(unsortedRoot);

        if (_opts.DedupEnabled)
            await BuildLibraryIndexAsync(outputFull, progress, ct);

        int done = 0, matched = 0, unsorted = 0, skipped = 0, deduped = 0, failed = 0;

        foreach (var file in files)
        {
            ct.ThrowIfCancellationRequested();
            done++;
            int pct = (int)(done * 100.0 / Math.Max(1, files.Count));
            Report(progress, pct, $"[{done}/{files.Count}] {Path.GetFileName(file)}");

            try
            {
                var info = AudioFile.Read(file);
                if (info == null)
                {
                    failed++;
                    Report(progress, pct, "  ! could not read file");
                    continue;
                }

                if (_opts.DedupEnabled && _opts.DedupByHash)
                    info.Sha1 = AudioFile.ComputeSha1(file);

                // Pure rename mode: skip the entire enrichment pipeline and just
                // rename based on the cleaned source filename. No API calls, no tag
                // changes — just a fast filename pass.
                bool renameOnly = _opts.Layout == FolderLayout.KeepInPlace
                                  && _opts.FileName == FileNamePattern.CleanedFilename;
                TrackMetadata meta;
                byte[]? cover;
                if (renameOnly)
                {
                    meta = info.Tags ?? new TrackMetadata { Source = "id3", Confidence = 0 };
                    cover = null;
                }
                else
                {
                    var (m, c, log) = await _pipeline.EnrichAsync(file, ct);
                    foreach (var l in log) Report(progress, pct, l);
                    meta = m; cover = c;
                }

                bool confident = renameOnly
                    || (meta.HasArtistAndTitle && meta.Confidence >= 0.55);

                // Stash the enriched metadata onto the AudioInfo so dedup keys are accurate.
                var enrichedInfo = new AudioInfo
                {
                    Path = file,
                    Tags = meta,
                    Bitrate = info.Bitrate,
                    Duration = info.Duration,
                    FileSize = info.FileSize,
                    Sha1 = info.Sha1
                };

                // Duplicate check (skip self-matches: when scanning source==output, the
                // file we're processing is already in the index, so FindMatch would hit
                // itself).
                if (_opts.DedupEnabled && _opts.DupAction == DuplicateAction.KeepBest)
                {
                    var hit = _index.FindMatch(enrichedInfo);
                    if (hit != null && AreSameFile(file, hit.Path)) hit = null;

                    if (hit != null)
                    {
                        deduped++;
                        var cmp = AudioFile.CompareQuality(enrichedInfo, hit);
                        if (cmp <= 0)
                        {
                            // Existing file is at least as good — drop the incoming one.
                            if (_opts.Move)
                            {
                                Report(progress, pct, $"  -> dup of '{Relative(_opts.Output, hit.Path)}' (kept {hit.Bitrate}kbps); incoming -> {(_opts.DupRecycle ? "Recycle Bin" : "deleted")}");
                                RecycleBin.Remove(file, _opts.DupRecycle);
                            }
                            else
                            {
                                Report(progress, pct, $"  -> dup of '{Relative(_opts.Output, hit.Path)}' (kept {hit.Bitrate}kbps); not copied (source untouched)");
                            }
                            continue;
                        }
                        else
                        {
                            // Incoming is better — remove the library file then place the new one.
                            Report(progress, pct, $"  -> dup, incoming wins ({enrichedInfo.Bitrate}kbps > {hit.Bitrate}kbps); '{Relative(_opts.Output, hit.Path)}' -> {(_opts.DupRecycle ? "Recycle Bin" : "deleted")}");
                            _index.Remove(hit);
                            RecycleBin.Remove(hit.Path, _opts.DupRecycle);
                            // Fall through to placement.
                        }
                    }
                }

                string dest;
                string fallbackTag = "";
                if (confident)
                {
                    dest = PathBuilder.BuildDestination(_opts.Output, file, _opts.Layout, _opts.FileName, meta);
                }
                else if (_opts.LetterBucketFallback)
                {
                    dest = PathBuilder.BuildLetterFallbackDestination(_opts.Output, file);
                    fallbackTag = "  (letter fallback — no confident match)";
                }
                else
                {
                    dest = PathBuilder.BuildUnsortedDestination(_opts.Output, file);
                }

                // Apply the per-folder cap (if any). May redirect to "Folder (2)" etc.
                // Confident matches respect the cap; _Unsorted does not (it's a triage
                // bucket, not a curated layout).
                if (confident) dest = _folderCap.Resolve(dest);

                // src == dst? Skip the move; still re-tag in place if asked.
                if (AreSameFile(file, dest))
                {
                    Report(progress, pct, "  -> already at destination, skipped");
                    Mp3TagService.WriteTags(dest, meta, cover, _opts.TagMode, _opts.ArtMode, confident);
                    enrichedInfo.Path = dest;
                    if (_opts.DedupEnabled) _index.Add(enrichedInfo);
                    if (confident) _folderCap.Confirm(dest);
                    skipped++;
                    continue;
                }

                if (_opts.SkipExisting && File.Exists(dest))
                {
                    Report(progress, pct, $"  -> destination exists, skipped: {Relative(_opts.Output, dest)}");
                    skipped++;
                    continue;
                }

                dest = PathBuilder.EnsureUnique(dest);
                Directory.CreateDirectory(Path.GetDirectoryName(dest)!);

                if (_opts.Move) File.Move(file, dest, overwrite: false);
                else            File.Copy(file, dest, overwrite: false);

                Mp3TagService.WriteTags(dest, meta, cover, _opts.TagMode, _opts.ArtMode, confident);

                enrichedInfo.Path = dest;
                if (_opts.DedupEnabled) _index.Add(enrichedInfo);
                if (confident) _folderCap.Confirm(dest);

                Report(progress, pct, confident
                    ? $"  -> {Relative(_opts.Output, dest)}    [{meta.Source}, conf {meta.Confidence:0.00}]"
                    : $"  -> {Relative(_opts.Output, dest)}{fallbackTag}");

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
        Report(progress, 100, $"== {matched} matched, {unsorted} unsorted, {skipped} skipped, {deduped} dup-removed, {failed} failed.");

        if (_opts.Move) PruneEmptyDirs(_opts.Source);
    }

    /// <summary>
    /// Walk the existing output library and build the dedup index. Hashes are computed
    /// only when <see cref="SortOptions.HashEntireLibrary"/> is enabled.
    /// </summary>
    private async Task BuildLibraryIndexAsync(string outputFull, IProgress<SorterProgress> progress, CancellationToken ct)
    {
        if (!Directory.Exists(outputFull)) return;

        var unsortedRoot = Path.Combine(outputFull, "_Unsorted");
        var existing = Directory.EnumerateFiles(outputFull, "*.mp3", SearchOption.AllDirectories)
                                .Where(p => !IsUnder(p, unsortedRoot))
                                .ToList();

        if (existing.Count == 0) return;
        Report(progress, 0, $"== Indexing existing library ({existing.Count} file(s))...");

        await Task.Run(() =>
        {
            int n = 0;
            foreach (var path in existing)
            {
                ct.ThrowIfCancellationRequested();
                n++;
                var info = AudioFile.Read(path);
                if (info == null) continue;
                if (_opts.DedupByHash && _opts.HashEntireLibrary)
                    info.Sha1 = AudioFile.ComputeSha1(path);
                _index.Add(info);

                if (n % 100 == 0)
                    Report(progress, 0, $"   indexed {n}/{existing.Count}");
            }
        }, ct);

        Report(progress, 0, $"   {_index.Count} library entries indexed.");
        Report(progress, 0, "");
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
