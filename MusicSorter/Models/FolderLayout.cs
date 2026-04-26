namespace MusicSorter.Models;

public enum FolderLayout
{
    ArtistAlbumTrack,
    ArtistTitle,
    GenreArtistAlbumTrack,
    AlbumArtistAlbumTrack,
    YearArtistAlbumTrack,
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
