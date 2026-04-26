using System.IO;
using System.Text;
using MusicSorter.Models;

namespace MusicSorter.Services;

public static class PathBuilder
{
    private static readonly char[] InvalidFileChars = Path.GetInvalidFileNameChars();
    private static readonly char[] InvalidPathChars = Path.GetInvalidPathChars();

    public static string BuildDestination(string outputRoot, FolderLayout layout, FileNamePattern pattern,
                                          TrackMetadata m, string sourceExtension)
    {
        var artist      = SafeSegment(m.Artist ?? "Unknown Artist");
        var albumArtist = SafeSegment(m.AlbumArtist ?? m.Artist ?? "Unknown Artist");
        var album       = SafeSegment(m.Album ?? "Unknown Album");
        var genre       = SafeSegment(m.Genre ?? "Unknown Genre");
        var year        = m.Year > 0 ? m.Year.ToString() : "Unknown Year";

        var folder = layout switch
        {
            FolderLayout.ArtistAlbumTrack       => Path.Combine(outputRoot, artist, album),
            FolderLayout.AlbumArtistAlbumTrack  => Path.Combine(outputRoot, albumArtist, album),
            FolderLayout.ArtistTitle            => Path.Combine(outputRoot, artist),
            FolderLayout.GenreArtistAlbumTrack  => Path.Combine(outputRoot, genre, artist, album),
            FolderLayout.YearArtistAlbumTrack   => Path.Combine(outputRoot, year, artist, album),
            FolderLayout.Flat                   => outputRoot,
            _                                    => Path.Combine(outputRoot, artist, album)
        };

        var fileName = BuildFileName(pattern, m, sourceExtension);
        return Path.Combine(folder, fileName);
    }

    public static string BuildUnsortedDestination(string outputRoot, string sourcePath)
    {
        var fname = SafeSegment(FilenameCleaner.Clean(Path.GetFileNameWithoutExtension(sourcePath)));
        if (string.IsNullOrWhiteSpace(fname)) fname = SafeSegment(Path.GetFileNameWithoutExtension(sourcePath));
        if (string.IsNullOrWhiteSpace(fname)) fname = "track";
        return Path.Combine(outputRoot, "_Unsorted", fname + Path.GetExtension(sourcePath));
    }

    private static string BuildFileName(FileNamePattern pattern, TrackMetadata m, string ext)
    {
        var title = SafeSegment(m.Title ?? "Unknown Title");
        var artist = SafeSegment(m.Artist ?? "Unknown Artist");
        var trackStr = m.TrackNumber > 0 ? m.TrackNumber.ToString("00") : "00";

        var name = pattern switch
        {
            FileNamePattern.TrackTitle        => $"{trackStr} - {title}",
            FileNamePattern.ArtistTitle       => $"{artist} - {title}",
            FileNamePattern.TrackArtistTitle  => $"{trackStr} - {artist} - {title}",
            FileNamePattern.TitleOnly         => title,
            _                                  => $"{trackStr} - {title}"
        };

        // Trim Windows max path safety (255 chars name).
        if (name.Length > 200) name = name[..200];
        return name + ext;
    }

    public static string SafeSegment(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return "";
        var sb = new StringBuilder(s.Length);
        foreach (var ch in s)
        {
            if (Array.IndexOf(InvalidFileChars, ch) >= 0 || ch == '/' || ch == '\\')
                sb.Append('_');
            else if (char.IsControl(ch))
                continue;
            else
                sb.Append(ch);
        }
        var cleaned = sb.ToString().Trim().Trim('.', ' ');
        // Avoid Windows reserved names.
        var reserved = new[] { "CON", "PRN", "AUX", "NUL",
            "COM1","COM2","COM3","COM4","COM5","COM6","COM7","COM8","COM9",
            "LPT1","LPT2","LPT3","LPT4","LPT5","LPT6","LPT7","LPT8","LPT9" };
        foreach (var r in reserved)
            if (cleaned.Equals(r, StringComparison.OrdinalIgnoreCase)) return "_" + cleaned;
        return cleaned.Length == 0 ? "_" : cleaned;
    }

    public static string EnsureUnique(string path)
    {
        if (!File.Exists(path)) return path;
        var dir = Path.GetDirectoryName(path) ?? "";
        var name = Path.GetFileNameWithoutExtension(path);
        var ext = Path.GetExtension(path);
        for (int i = 2; i < 1000; i++)
        {
            var candidate = Path.Combine(dir, $"{name} ({i}){ext}");
            if (!File.Exists(candidate)) return candidate;
        }
        return path;
    }
}
