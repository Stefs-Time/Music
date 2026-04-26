using MusicSorter.Models;

namespace MusicSorter.Services;

/// <summary>
/// Cascading enrichment: runs every enabled source and merges results, keeping the
/// highest-confidence values per field.
/// </summary>
public sealed class EnrichmentPipeline
{
    private readonly SortOptions _opts;
    private readonly MusicBrainzClient _mb = new();
    private readonly AcoustIdClient _acoustId;
    private readonly ChromaprintRunner _fpcalc;
    private readonly ShazamClient _shazam;
    private readonly CoverArtClient _cover = new();

    public EnrichmentPipeline(SortOptions opts)
    {
        _opts = opts;
        _acoustId = new AcoustIdClient(opts.AcoustIdKey);
        _fpcalc   = new ChromaprintRunner(opts.FpcalcPath);
        _shazam   = new ShazamClient(opts.ShazamKey, opts.ShazamHost);
    }

    public async Task<(TrackMetadata meta, byte[]? coverArt, List<string> log)> EnrichAsync(string filePath, CancellationToken ct)
    {
        var log = new List<string>();
        TrackMetadata best = new() { Source = "none", Confidence = 0 };

        // 0. Existing ID3 (so we don't blow away good tags).
        var id3 = Mp3TagService.ReadExisting(filePath);
        if (id3 != null && id3.HasArtistAndTitle)
        {
            best = Merge(best, id3);
            log.Add($"  id3       -> {id3}");
        }

        // 1. Filename cleanup.
        if (_opts.UseClean)
        {
            var fn = FilenameCleaner.CleanFromFileName(filePath);
            if (fn.Title != null) log.Add($"  filename  -> {fn}");
            best = Merge(best, fn);
        }

        // 2. MusicBrainz title/artist search.
        if (_opts.UseMusicBrainz && !string.IsNullOrWhiteSpace(best.Title))
        {
            try
            {
                var mb = await _mb.SearchByArtistTitleAsync(best.Artist, best.Title, ct);
                if (mb != null)
                {
                    log.Add($"  mbrainz   -> {mb}  (score {mb.Confidence:0.00})");
                    best = Merge(best, mb);
                }
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) { log.Add($"  mbrainz   ! {ex.Message}"); }
        }

        // 3. AcoustID (only if we still aren't confident).
        if (_opts.UseAcoustId && _acoustId.Configured && best.Confidence < 0.85)
        {
            if (!_fpcalc.Available)
            {
                log.Add("  acoustid  ! fpcalc.exe not found - skipping");
            }
            else
            {
                try
                {
                    var fp = await _fpcalc.FingerprintAsync(filePath, ct);
                    if (fp.HasValue)
                    {
                        var ac = await _acoustId.LookupAsync(fp.Value.Fingerprint, fp.Value.Duration, ct);
                        if (ac != null)
                        {
                            log.Add($"  acoustid  -> {ac}  (score {ac.Confidence:0.00})");
                            best = Merge(best, ac);
                        }
                        else log.Add("  acoustid  -> no match");
                    }
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex) { log.Add($"  acoustid  ! {ex.Message}"); }
            }
        }

        // 4. Shazam (text search by best.Artist+Title).
        if (_opts.UseShazam && _shazam.Configured && best.Confidence < 0.95)
        {
            try
            {
                var sh = await _shazam.SearchAsync(best.Artist, best.Title, ct);
                if (sh != null)
                {
                    log.Add($"  shazam    -> {sh}");
                    best = Merge(best, sh);
                }
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) { log.Add($"  shazam    ! {ex.Message}"); }
        }

        // 5. Promote MB ids from id3 onto best if we never got any.
        if (best.MusicBrainzReleaseId == null && id3?.MusicBrainzReleaseId != null)
            best.MusicBrainzReleaseId = id3.MusicBrainzReleaseId;

        // 6. Album art (Cover Art Archive by release MBID, then by release-group MBID,
        //    then a direct image URL if Shazam gave us one). Skipped when the user
        //    chose Keep-existing or Strip — no point downloading art we won't write.
        byte[]? cover = null;
        if (_opts.ArtMode == CoverArtMode.EmbedDownloaded)
        {
            try
            {
                if (cover == null && !string.IsNullOrEmpty(best.MusicBrainzReleaseId))
                {
                    cover = await _cover.FetchByReleaseMbidAsync(best.MusicBrainzReleaseId!, ct);
                    if (cover != null) log.Add("  coverart  -> CoverArtArchive (release)");
                }
                if (cover == null && !string.IsNullOrEmpty(best.MusicBrainzReleaseGroupId))
                {
                    cover = await _cover.FetchByReleaseGroupMbidAsync(best.MusicBrainzReleaseGroupId!, ct);
                    if (cover != null) log.Add("  coverart  -> CoverArtArchive (release-group)");
                }
                if (cover == null && !string.IsNullOrEmpty(best.AlbumArtUrl))
                {
                    cover = await _cover.FetchByUrlAsync(best.AlbumArtUrl!, ct);
                    if (cover != null) log.Add("  coverart  -> Shazam image");
                }
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) { log.Add($"  coverart  ! {ex.Message}"); }
        }

        return (best, cover, log);
    }

    /// <summary>Take fields from <paramref name="b"/> when its confidence is higher
    /// or when the existing field is empty.</summary>
    private static TrackMetadata Merge(TrackMetadata a, TrackMetadata b)
    {
        bool prefer = b.Confidence >= a.Confidence;

        var r = a.Clone();
        r.Title       = Pick(a.Title,       b.Title,       prefer);
        r.Artist      = Pick(a.Artist,      b.Artist,      prefer);
        r.AlbumArtist = Pick(a.AlbumArtist, b.AlbumArtist, prefer);
        r.Album       = Pick(a.Album,       b.Album,       prefer);
        r.Genre       = Pick(a.Genre,       b.Genre,       prefer);

        if (b.Year > 0 && (a.Year == 0 || prefer))                 r.Year = b.Year;
        if (b.TrackNumber > 0 && (a.TrackNumber == 0 || prefer))   r.TrackNumber = b.TrackNumber;
        if (b.TrackCount > 0 && (a.TrackCount == 0 || prefer))     r.TrackCount = b.TrackCount;
        if (b.Disc > 0 && (a.Disc == 0 || prefer))                 r.Disc = b.Disc;
        if (!string.IsNullOrEmpty(b.MusicBrainzReleaseId))      r.MusicBrainzReleaseId      = b.MusicBrainzReleaseId;
        if (!string.IsNullOrEmpty(b.MusicBrainzReleaseGroupId)) r.MusicBrainzReleaseGroupId = b.MusicBrainzReleaseGroupId;
        if (!string.IsNullOrEmpty(b.MusicBrainzRecordingId))    r.MusicBrainzRecordingId    = b.MusicBrainzRecordingId;
        if (!string.IsNullOrEmpty(b.AlbumArtUrl))               r.AlbumArtUrl               = b.AlbumArtUrl;

        if (b.Confidence > r.Confidence)
        {
            r.Confidence = b.Confidence;
            r.Source = b.Source;
        }
        return r;
    }

    private static string? Pick(string? a, string? b, bool prefer)
    {
        if (prefer && !string.IsNullOrWhiteSpace(b)) return b;
        if (!string.IsNullOrWhiteSpace(a)) return a;
        return b;
    }
}
