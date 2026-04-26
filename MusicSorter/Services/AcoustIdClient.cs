using System.Net.Http;
using System.Text.Json;
using MusicSorter.Models;

namespace MusicSorter.Services;

/// <summary>
/// AcoustID lookup.  Sends Chromaprint fingerprint + duration, gets back a recording-id
/// and rich metadata.  https://acoustid.org/webservice
/// </summary>
public sealed class AcoustIdClient
{
    private readonly string _key;
    public AcoustIdClient(string apiKey) => _key = apiKey;
    public bool Configured => !string.IsNullOrWhiteSpace(_key);

    public async Task<TrackMetadata?> LookupAsync(string fingerprint, int duration, CancellationToken ct)
    {
        if (!Configured) return null;

        var url = "https://api.acoustid.org/v2/lookup"
                + $"?client={Uri.EscapeDataString(_key)}"
                + $"&duration={duration}"
                + $"&fingerprint={Uri.EscapeDataString(fingerprint)}"
                + "&meta=recordings+releasegroups+releases+tracks+compress";

        using var resp = await SharedHttp.Client.GetAsync(url, ct);
        if (!resp.IsSuccessStatusCode) return null;

        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
        if (!doc.RootElement.TryGetProperty("status", out var st) || st.GetString() != "ok") return null;
        if (!doc.RootElement.TryGetProperty("results", out var results) || results.GetArrayLength() == 0) return null;

        // Pick the highest scoring result that actually has recordings.
        TrackMetadata? best = null;
        foreach (var r in results.EnumerateArray())
        {
            var score = r.TryGetProperty("score", out var s) ? s.GetDouble() : 0.0;
            if (!r.TryGetProperty("recordings", out var recs) || recs.GetArrayLength() == 0) continue;

            foreach (var rec in recs.EnumerateArray())
            {
                var meta = MapRecording(rec, score);
                if (meta == null) continue;
                if (best == null || meta.Confidence > best.Confidence) best = meta;
            }
        }
        return best;
    }

    private static TrackMetadata? MapRecording(JsonElement rec, double score)
    {
        if (!rec.TryGetProperty("title", out var t) || t.GetString() is not string title)
            return null;

        string? artist = null;
        if (rec.TryGetProperty("artists", out var arts) && arts.ValueKind == JsonValueKind.Array)
        {
            var bits = new List<string>();
            foreach (var a in arts.EnumerateArray())
            {
                if (a.TryGetProperty("name", out var n) && n.GetString() is string nm) bits.Add(nm);
                if (a.TryGetProperty("joinphrase", out var jp) && jp.GetString() is string j && !string.IsNullOrEmpty(j))
                    bits.Add(j);
            }
            artist = string.Concat(bits).Trim();
        }

        string? album = null, releaseId = null;
        uint year = 0, trackNo = 0, trackCount = 0;
        if (rec.TryGetProperty("releasegroups", out var rgs) && rgs.ValueKind == JsonValueKind.Array && rgs.GetArrayLength() > 0)
        {
            var rg = rgs[0];
            if (rg.TryGetProperty("title", out var rt)) album = rt.GetString();
            if (rg.TryGetProperty("id", out var rid)) releaseId = rid.GetString();
            if (rg.TryGetProperty("releases", out var rels) && rels.ValueKind == JsonValueKind.Array && rels.GetArrayLength() > 0)
            {
                var rel = rels[0];
                if (rel.TryGetProperty("date", out var d) && d.TryGetProperty("year", out var yr) && yr.TryGetInt32(out var yv))
                    year = (uint)yv;
                if (rel.TryGetProperty("mediums", out var meds) && meds.ValueKind == JsonValueKind.Array)
                {
                    foreach (var m in meds.EnumerateArray())
                    {
                        if (m.TryGetProperty("track_count", out var tc) && tc.TryGetInt32(out var tcv))
                            trackCount = (uint)tcv;
                        if (m.TryGetProperty("tracks", out var trs) && trs.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var tr in trs.EnumerateArray())
                            {
                                if (tr.TryGetProperty("position", out var pos) && pos.TryGetInt32(out var p))
                                    trackNo = (uint)p;
                            }
                        }
                    }
                }
            }
        }

        return new TrackMetadata
        {
            Title = title,
            Artist = string.IsNullOrWhiteSpace(artist) ? null : artist,
            AlbumArtist = string.IsNullOrWhiteSpace(artist) ? null : artist,
            Album = album,
            Year = year,
            TrackNumber = trackNo,
            TrackCount = trackCount,
            MusicBrainzReleaseId = releaseId,
            MusicBrainzRecordingId = rec.TryGetProperty("id", out var id) ? id.GetString() : null,
            Confidence = Math.Min(1.0, 0.6 + score * 0.4),
            Source = "acoustid"
        };
    }
}
