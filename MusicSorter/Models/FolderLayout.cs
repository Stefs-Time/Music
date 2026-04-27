namespace MusicSorter.Models;

public enum FolderLayout
{
    ArtistAlbumTrack,
    ArtistTitle,
    GenreArtistAlbumTrack,
    AlbumArtistAlbumTrack,
    YearArtistAlbumTrack,
    /// <summary>A / Abba / Dancing Queen.mp3 — first letter of artist, then artist
    /// folder, then the file. "The Beatles" goes under B (leading "The " is stripped).
    /// Non-letter artists go under "0-9" (digits) or "#" (anything else).</summary>
    LetterArtistTitle,
    /// <summary>A / Abba / Arrival / 03 - Dancing Queen.mp3 — same letter bucketing
    /// but with the album subfolder kept.</summary>
    LetterArtistAlbumTrack,
    Flat
}

public enum FileNamePattern
{
    TrackTitle,
    ArtistTitle,
    TrackArtistTitle,
    TitleOnly
}

public sealed record FolderLayoutOption(FolderLayout Layout, string Label);
public sealed record FileNameOption(FileNamePattern Pattern, string Label);

public static class FolderLayoutOptions
{
    public static readonly IReadOnlyList<FolderLayoutOption> Layouts = new[]
    {
        new FolderLayoutOption(FolderLayout.ArtistAlbumTrack,        "Artist / Album / <file>"),
        new FolderLayoutOption(FolderLayout.AlbumArtistAlbumTrack,   "AlbumArtist / Album / <file>"),
        new FolderLayoutOption(FolderLayout.ArtistTitle,             "Artist / <file>"),
        new FolderLayoutOption(FolderLayout.LetterArtistTitle,       "Letter / Artist / <file>          (A / Abba / ...)"),
        new FolderLayoutOption(FolderLayout.LetterArtistAlbumTrack,  "Letter / Artist / Album / <file>  (A / Abba / Arrival / ...)"),
        new FolderLayoutOption(FolderLayout.GenreArtistAlbumTrack,   "Genre / Artist / Album / <file>"),
        new FolderLayoutOption(FolderLayout.YearArtistAlbumTrack,    "Year / Artist / Album / <file>"),
        new FolderLayoutOption(FolderLayout.Flat,                    "Flat (no subfolders)")
    };

    public static readonly IReadOnlyList<FileNameOption> FileNames = new[]
    {
        new FileNameOption(FileNamePattern.TrackTitle,        "{track:00} - {title}.mp3"),
        new FileNameOption(FileNamePattern.ArtistTitle,       "{artist} - {title}.mp3"),
        new FileNameOption(FileNamePattern.TrackArtistTitle,  "{track:00} - {artist} - {title}.mp3"),
        new FileNameOption(FileNamePattern.TitleOnly,         "{title}.mp3")
    };
}
