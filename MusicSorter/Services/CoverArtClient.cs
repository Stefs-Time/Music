using System.Net.Http;

namespace MusicSorter.Services;

/// <summary>
/// Fetches album art bytes either from MusicBrainz Cover Art Archive (by release-mbid)
/// or from a direct Shazam image URL.
/// </summary>
public sealed class CoverArtClient
{
    public async Task<byte[]?> FetchByReleaseMbidAsync(string mbid, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(mbid)) return null;
        var url = $"https://coverartarchive.org/release/{mbid}/front-500";
        return await TryGetBytesAsync(url, ct);
    }

    public async Task<byte[]?> FetchByReleaseGroupMbidAsync(string mbid, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(mbid)) return null;
        var url = $"https://coverartarchive.org/release-group/{mbid}/front-500";
        return await TryGetBytesAsync(url, ct);
    }

    public Task<byte[]?> FetchByUrlAsync(string url, CancellationToken ct) => TryGetBytesAsync(url, ct);

    private static async Task<byte[]?> TryGetBytesAsync(string url, CancellationToken ct)
    {
        try
        {
            using var resp = await SharedHttp.Client.GetAsync(url, ct);
            if (!resp.IsSuccessStatusCode) return null;
            return await resp.Content.ReadAsByteArrayAsync(ct);
        }
        catch
        {
            return null;
        }
    }
}
