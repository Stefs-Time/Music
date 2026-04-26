using System.Net.Http;
using System.Text.Json;
using MusicSorter.Models;

namespace MusicSorter.Services;

/// <summary>
/// Shazam text search via RapidAPI (<c>/search?term=...</c>). Returns Shazam's rich
/// metadata (album, genre, year, cover-art URL) for tracks where MusicBrainz is missing
/// data — e.g. modern singles that haven't propagated to MB yet.
///
/// True Shazam audio recognition (POST /songs/v2/detect with a base64 PCM sample) is
/// not implemented because it would require bundling an MP3 decoder to produce the
/// 16-bit / 44.1 kHz / mono sample Shazam expects.
/// </summary>
public sealed class ShazamClient
{
    private readonly string _key;
    private readonly string _host;
    public bool Configured => !string.IsNullOrWhiteSpace(_key);

    public ShazamClient(string apiKey, string host)
    {
        _key = apiKey ?? "";
        _host = string.IsNullOrWhiteSpace(host) ? "shazam.p.rapidapi.com" : host.Trim();
    }

    public async Task<TrackMetadata?> SearchAsync(string? artist, string? title, CancellationToken ct)
    {
        if (!Configured) return null;
        if (string.IsNullOrWhiteSpace(title)) return null;

        var term = string.IsNullOrWhiteSpace(artist) ? title! : $"{artist} {title}";
        var url = $"https://{_host}/search?term={Uri.EscapeDataString(term)}&locale=en-US&offset=0&limit=5";

        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.Add("x-rapidapi-host", _host);
        req.Headers.Add("x-rapidapi-key", _key);

        using var resp = await SharedHttp.Client.SendAsync(req, ct);
        if (!resp.IsSuccessStatusCode) return null;

        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));

        // Shazam search shape: tracks.hits[].track.{title, subtitle, sections[], images}
        if (!doc.RootElement.TryGetProperty("tracks", out var tracks)) return null;
        if (!tracks.TryGetProperty("hits", out var hits) || hits.GetArrayLength() == 0) return null;

        var hit = hits[0];
        if (!hit.TryGetProperty("track", out var tr)) return null;

        string? sTitle = tr.TryGetProperty("title", out var t) ? t.GetString() : null;
        string? sArtist = tr.TryGetProperty("subtitle", out var sub) ? sub.GetString() : null;
        if (string.IsNullOrWhiteSpace(sTitle)) return null;

        string? album = null, genre = null;
        uint year = 0;

        if (tr.TryGetProperty("sections", out var sections) && sections.ValueKind == JsonValueKind.Array)
        {
            foreach (var sec in sections.EnumerateArray())
            {
                var type = sec.TryGetProperty("type", out var ty) ? ty.GetString() : null;
                if (type != "SONG") continue;
                if (!sec.TryGetProperty("metadata", out var meta) || meta.ValueKind != JsonValueKind.Array) continue;

                foreach (var kv in meta.EnumerateArray())
                {
                    var title2 = kv.TryGetProperty("title", out var tt) ? tt.GetString() : null;
                    var text2  = kv.TryGetProperty("text",  out var tx) ? tx.GetString() : null;
                    if (string.IsNullOrEmpty(text2)) continue;
                    switch (title2)
                    {
                        case "Album":   album = text2; break;
                        case "Released": if (uint.TryParse(text2, out var y)) year = y; break;
                        case "Genre":   genre = text2; break;
                    }
                }
            }
        }
        if (genre is null && tr.TryGetProperty("genres", out var gen) &&
            gen.TryGetProperty("primary", out var gp))
            genre = gp.GetString();

        string? artUrl = null;
        if (tr.TryGetProperty("images", out var imgs))
        {
            foreach (var key in new[] { "coverarthq", "coverart", "background" })
                if (imgs.TryGetProperty(key, out var iv) && iv.GetString() is string s && !string.IsNullOrEmpty(s))
                { artUrl = s; break; }
        }

        return new TrackMetadata
        {
            Title = sTitle,
            Artist = sArtist,
            AlbumArtist = sArtist,
            Album = album,
            Year = year,
            Genre = genre,
            AlbumArtUrl = artUrl,
            Confidence = 0.80,
            Source = "shazam"
        };
    }
}
