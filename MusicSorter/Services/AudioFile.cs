using System.IO;
using System.Security.Cryptography;
using MusicSorter.Models;

namespace MusicSorter.Services;

/// <summary>Lightweight bag of facts read from a single MP3 file.</summary>
public sealed class AudioInfo
{
    public required string Path { get; init; }
    public TrackMetadata? Tags { get; init; }
    public int Bitrate { get; init; }       // kbps
    public TimeSpan Duration { get; init; }
    public long FileSize { get; init; }
    public string? Sha1 { get; set; }       // computed lazily

    /// <summary>Quality ordering: bitrate > size > duration. Higher is better.</summary>
    public (int bitrate, long size, long durMs) QualityKey =>
        (Bitrate, FileSize, (long)Duration.TotalMilliseconds);
}

public static class AudioFile
{
    public static AudioInfo? Read(string path)
    {
        try
        {
            using var f = TagLib.File.Create(path);
            var fi = new FileInfo(path);
            return new AudioInfo
            {
                Path = path,
                Tags = Mp3TagService.ReadExisting(path),
                Bitrate = f.Properties?.AudioBitrate ?? 0,
                Duration = f.Properties?.Duration ?? TimeSpan.Zero,
                FileSize = fi.Exists ? fi.Length : 0
            };
        }
        catch
        {
            return null;
        }
    }

    public static string ComputeSha1(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            var hash = SHA1.HashData(stream);
            return Convert.ToHexString(hash);
        }
        catch
        {
            return "";
        }
    }

    /// <summary>Return -1 if a is worse than b, +1 if better, 0 if equal.</summary>
    public static int CompareQuality(AudioInfo a, AudioInfo b)
    {
        var ak = a.QualityKey; var bk = b.QualityKey;
        if (ak.bitrate != bk.bitrate) return ak.bitrate.CompareTo(bk.bitrate);
        if (ak.size    != bk.size)    return ak.size.CompareTo(bk.size);
        return ak.durMs.CompareTo(bk.durMs);
    }
}
