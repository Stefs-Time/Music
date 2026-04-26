using System.Net.Http;
using System.Text.Json;
using MusicSorter.Models;

namespace MusicSorter.Services;

/// <summary>
/// MusicBrainz REST lookups. Free, no API key. We call the recording search endpoint and pick
/// the best release.  https://musicbrainz.org/doc/MusicBrainz_API
/// </summary>
public sealed class MusicBrainzClient
{
    private const string Base = "https://musicbrainz.org/ws/2";
    private DateTime _nextAllowed = DateTime.MinValue;

    public async Task<TrackMetadata?> SearchByArtistTitleAsync(string? artist, string? title, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(title)) return null;

        var query = string.IsNullOrWhiteSpace(artist)
            ? $"recording:\"{Escape(title)}\""
            : $"recording:\"{Escape(title)}\" AND artist:\"{Escape(artist)}\"";

        var url = $"{Base}/recording/?query={Uri.EscapeDataString(query)}&fmt=json&limit=5";
        return await QueryAsync(url, ct);
    }

    public async Task<TrackMetadata?> LookupByRecordingIdAsync(string mbid, CancellationToken ct)
    {
        var url = $"{Base}/recording/{mbid}?inc=releases+release-groups+artist-credits+tags&fmt=json";
        await ThrottleAsync(ct);
        using var resp = await SharedHttp.Client.GetAsync(url, ct);
        if (!resp.IsSuccessStatusCode) return null;
        var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
        return MapRecording(doc.RootElement, scoreFloor: 0.85);
    }

    private async Task<TrackMetadata?> QueryAsync(string url, CancellationToken ct)
    {
        await ThrottleAsync(ct);
        using var resp = await SharedHttp.Client.GetAsync(url, ct);
        if (!resp.IsSuccessStatusCode) return null;

        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
        if (!doc.RootElement.TryGetProperty("recordings", out var recs) || recs.GetArrayLength() == 0)
            return null;

        TrackMetadata? best = null;
        foreach (var rec in recs.EnumerateArray())
        {
            var m = MapRecording(rec, scoreFloor: 0.0);
            if (m == null) continue;
            if (best == null || m.Confidence > best.Confidence) best = m;
        }
        return best;
    }

    private static TrackMetadata? MapRecording(JsonElement rec, double scoreFloor)
    {
        if (rec.ValueKind != JsonValueKind.Object) return null;

        var score = rec.TryGetProperty("score", out var s) ? s.GetInt32() / 100.0 : scoreFloor;
        var title = rec.TryGetProperty("title", out var t) ? t.GetString() : null;
        if (string.IsNullOrWhiteSpace(title)) return null;

        string? artist = null, albumArtist = null;
        if (rec.TryGetProperty("artist-credit", out var ac) && ac.ValueKind == JsonValueKind.Array)
        {
            var bits = new List<string>();
            foreach (var a in ac.EnumerateArray())
            {
                if (a.TryGetProperty("name", out var n) && n.GetString() is string nm) bits.Add(nm);
                else if (a.TryGetProperty("artist", out var ar) &&
                         ar.TryGetProperty("name", out var n2) && n2.GetString() is string nm2) bits.Add(nm2);
                if (a.TryGetProperty("joinphrase", out var jp) && jp.GetString() is string j && !string.IsNullOrEmpty(j))
                    bits.Add(j);
            }
            artist = string.Concat(bits).Trim();
            if (ac.GetArrayLength() > 0 &&
                ac[0].TryGetProperty("artist", out var first) &&
                first.TryGetProperty("name", out var fn))
                albumArtist = fn.GetString();
        }

        string? album = null, releaseId = null;
        uint year = 0, trackNo = 0, trackCount = 0;
        if (rec.TryGetProperty("releases", out var rels) && rels.ValueKind == JsonValueKind.Array && rels.GetArrayLength() > 0)
        {
            // Prefer the earliest official album release.
            JsonElement chosen = default;
            DateTime bestDate = DateTime.MaxValue;
            foreach (var r in rels.EnumerateArray())
            {
                var status = r.TryGetProperty("status", out var st) ? st.GetString() : null;
                if (status != null && !status.Equals("Official", StringComparison.OrdinalIgnoreCase)) continue;
                var date = ParseDate(r);
                if (date < bestDate) { bestDate = date; chosen = r; }
            }
            if (chosen.ValueKind == JsonValueKind.Undefined) chosen = rels[0];

            if (chosen.TryGetProperty("title", out var rt)) album = rt.GetString();
            if (chosen.TryGetProperty("id", out var rid)) releaseId = rid.GetString();
            year = (uint)Math.Max(0, ParseDate(chosen).Year);

            if (chosen.TryGetProperty("media", out var media) && media.ValueKind == JsonValueKind.Array)
            {
                foreach (var med in media.EnumerateArray())
                {
                    if (med.TryGetProperty("track-count", out var tc) && tc.TryGetInt32(out var tcv))
                        trackCount = (uint)tcv;
                    if (med.TryGetProperty("track", out var tracks) && tracks.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var tr in tracks.EnumerateArray())
                        {
                            if (tr.TryGetProperty("number", out var num) &&
                                uint.TryParse(num.GetString(), out var n)) trackNo = n;
                        }
                    }
                }
            }
        }

        string? genre = null;
        if (rec.TryGetProperty("tags", out var tags) && tags.ValueKind == JsonValueKind.Array && tags.GetArrayLength() > 0)
        {
            JsonElement bestTag = default;
            int bestCount = -1;
            foreach (var tg in tags.EnumerateArray())
            {
                int count = tg.TryGetProperty("count", out var cnt) && cnt.TryGetInt32(out var cv) ? cv : 0;
                if (count > bestCount) { bestCount = count; bestTag = tg; }
            }
            if (bestTag.ValueKind == JsonValueKind.Object && bestTag.TryGetProperty("name", out var nm))
                genre = nm.GetString();
        }

        if (score < scoreFloor) return null;

        return new TrackMetadata
        {
            Title = title,
            Artist = NullIfEmpty(artist),
            AlbumArtist = NullIfEmpty(albumArtist),
            Album = album,
            Year = year,
            TrackNumber = trackNo,
            TrackCount = trackCount,
            Genre = genre,
            MusicBrainzReleaseId = releaseId,
            MusicBrainzRecordingId = rec.TryGetProperty("id", out var id) ? id.GetString() : null,
            Confidence = score,
            Source = "musicbrainz"
        };
    }

    private static DateTime ParseDate(JsonElement r)
    {
        if (r.TryGetProperty("date", out var d) && d.GetString() is string ds && !string.IsNullOrWhiteSpace(ds))
        {
            // YYYY, YYYY-MM, or YYYY-MM-DD all parse with flexible options.
            if (DateTime.TryParse(ds, System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal,
                out var parsed))
                return parsed;
            if (int.TryParse(ds.AsSpan(0, Math.Min(4, ds.Length)), out var y))
                return new DateTime(y, 1, 1);
        }
        return DateTime.MaxValue;
    }

    private static string Escape(string s) =>
        s.Replace("\\", "\\\\").Replace("\"", "\\\"");

    private static string? NullIfEmpty(string? s) => string.IsNullOrWhiteSpace(s) ? null : s;

    private async Task ThrottleAsync(CancellationToken ct)
    {
        // MusicBrainz asks for ~1 req/s.
        var wait = _nextAllowed - DateTime.UtcNow;
        if (wait > TimeSpan.Zero) await Task.Delay(wait, ct);
        _nextAllowed = DateTime.UtcNow + TimeSpan.FromMilliseconds(1100);
    }
}
