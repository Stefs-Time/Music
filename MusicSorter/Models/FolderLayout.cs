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
    /// <summary>A / Abba - Dancing Queen.mp3 — letter bucket only, no artist
    /// subfolder. Pair with the per-folder cap (e.g. 99) so a busy letter splits
    /// into "A", "A (2)", "A (3)", ... instead of one huge directory.</summary>
    LetterTitle,
    /// <summary>Don't move the file. Just rename it in its current folder. Useful
    /// for a quick "clean the filenames" pass with no library reorganisation.</summary>
    KeepInPlace,
    Flat
}

public enum FileNamePattern
{
    TrackTitle,
    ArtistTitle,
    TrackArtistTitle,
    TitleOnly,
    /// <summary>Use the source filename with YouTube cruft stripped, no parsing.
    /// Pair with FolderLayout.KeepInPlace for a pure "clean the names" pass.</summary>
    CleanedFilename
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
        new FolderLayoutOption(FolderLayout.LetterTitle,             "Letter / <file>                   (A / Abba - Dancing Queen.mp3)"),
        new FolderLayoutOption(FolderLayout.KeepInPlace,             "Rename in place                   (don't move; just rename in source folder)"),
        new FolderLayoutOption(FolderLayout.GenreArtistAlbumTrack,   "Genre / Artist / Album / <file>"),
        new FolderLayoutOption(FolderLayout.YearArtistAlbumTrack,    "Year / Artist / Album / <file>"),
        new FolderLayoutOption(FolderLayout.Flat,                    "Flat (no subfolders)")
    };

    public static readonly IReadOnlyList<FileNameOption> FileNames = new[]
    {
        new FileNameOption(FileNamePattern.TrackTitle,        "{track:00} - {title}.mp3"),
        new FileNameOption(FileNamePattern.ArtistTitle,       "{artist} - {title}.mp3"),
        new FileNameOption(FileNamePattern.TrackArtistTitle,  "{track:00} - {artist} - {title}.mp3"),
        new FileNameOption(FileNamePattern.TitleOnly,         "{title}.mp3"),
        new FileNameOption(FileNamePattern.CleanedFilename,   "{cleaned source filename}.mp3 (no metadata)")
    };
}
