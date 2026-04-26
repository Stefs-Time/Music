using System.Text;
using MusicSorter.Models;

namespace MusicSorter.Services;

/// <summary>
/// Index of every track currently considered "in the library" (existing scan + everything
/// we've placed during this run). Looks up by any of the enabled keys.
/// </summary>
public sealed class DuplicateIndex
{
    private readonly SortOptions _opts;
    private readonly Dictionary<string, AudioInfo> _byKey = new(StringComparer.Ordinal);

    public DuplicateIndex(SortOptions opts) => _opts = opts;

    public int Count => _byKey.Values.Distinct().Count();

    /// <summary>Register all keys for this entry. Hash is included only if non-empty.</summary>
    public void Add(AudioInfo info)
    {
        foreach (var key in KeysFor(info, registering: true))
            _byKey[key] = info;
    }

    public void Remove(AudioInfo info)
    {
        foreach (var key in KeysFor(info, registering: true))
            if (_byKey.TryGetValue(key, out var v) && ReferenceEquals(v, info))
                _byKey.Remove(key);
    }

    /// <summary>
    /// Return the first existing entry that collides with <paramref name="info"/> on any
    /// enabled key, or null if none.
    /// </summary>
    public AudioInfo? FindMatch(AudioInfo info)
    {
        if (!_opts.DedupEnabled) return null;
        foreach (var key in KeysFor(info, registering: false))
            if (_byKey.TryGetValue(key, out var hit)) return hit;
        return null;
    }

    private IEnumerable<string> KeysFor(AudioInfo info, bool registering)
    {
        var m = info.Tags;

        if (_opts.DedupByArtistTitle && m != null &&
            !string.IsNullOrWhiteSpace(m.Artist) && !string.IsNullOrWhiteSpace(m.Title))
        {
            yield return "AT|" + Norm(m.Artist!) + "|" + Norm(m.Title!);
        }

        if (_opts.DedupByMbid && m != null && !string.IsNullOrWhiteSpace(m.MusicBrainzRecordingId))
        {
            yield return "MB|" + m.MusicBrainzRecordingId;
        }

        if (_opts.DedupByDuration && m != null &&
            !string.IsNullOrWhiteSpace(m.Artist) && info.Duration > TimeSpan.Zero)
        {
            int sec = (int)Math.Round(info.Duration.TotalSeconds);
            // When ADDING: register only one bucket. When LOOKING UP: try ±2 to allow
            // a 2-second tolerance between two encodings of the same song.
            if (registering)
                yield return "AD|" + Norm(m.Artist!) + "|" + sec;
            else
                for (int delta = -2; delta <= 2; delta++)
                    yield return "AD|" + Norm(m.Artist!) + "|" + (sec + delta);
        }

        if (_opts.DedupByHash && !string.IsNullOrEmpty(info.Sha1))
        {
            yield return "H|" + info.Sha1;
        }
    }

    private static string Norm(string s)
    {
        var sb = new StringBuilder(s.Length);
        foreach (var ch in s.ToLowerInvariant())
            if (char.IsLetterOrDigit(ch)) sb.Append(ch);
        return sb.ToString();
    }
}
