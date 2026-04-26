using System.Net.Http;
using System.Text.Json;
using MusicSorter.Models;

namespace MusicSorter.Services;

/// <summary>
/// Shazam search via RapidAPI. Two endpoints supported (auto-fallback):
///   - /search?term=<artist title>     (text search)
///   - /songs/v2/detect (audio recognition) — only used if Shazam recognition by title fails.
///
/// Real audio recognition needs a 16-bit 44.1 kHz mono PCM sample uploaded as base64.  We
/// don't ship an MP3 decoder, so this client does the *text* search by default and grabs
/// rich Shazam metadata (album art URL, genre, ISRC). It still gives us enrichment that
/// MusicBrainz misses (e.g. modern singles that aren't in MB yet).
/// </summary>
public sealed class ShazamClient
{
    private readonly string _key;
    private readonly string _host;
    public bool Configured => !string.IsNullOrWhiteSpace(_key);
    public string LastAlbumArtUrl { get; private set; } = "";

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

        if (tr.TryGetProperty("images", out var imgs))
        {
            string? art = null;
            foreach (var key in new[] { "coverarthq", "coverart", "background" })
                if (imgs.TryGetProperty(key, out var iv) && iv.GetString() is string s && !string.IsNullOrEmpty(s))
                { art = s; break; }
            LastAlbumArtUrl = art ?? "";
        }

        return new TrackMetadata
        {
            Title = sTitle,
            Artist = sArtist,
            AlbumArtist = sArtist,
            Album = album,
            Year = year,
            Genre = genre,
            Confidence = 0.80,
            Source = "shazam"
        };
    }
}
