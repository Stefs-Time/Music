using MusicSorter.Models;

namespace MusicSorter.Services;

/// <summary>
/// Reads existing ID3 tags + writes enriched metadata using TagLibSharp.
/// </summary>
public static class Mp3TagService
{
    public static TrackMetadata? ReadExisting(string path)
    {
        try
        {
            using var f = TagLib.File.Create(path);
            var tag = f.Tag;
            if (tag == null) return null;

            var meta = new TrackMetadata
            {
                Title = string.IsNullOrWhiteSpace(tag.Title) ? null : tag.Title,
                Artist = string.IsNullOrWhiteSpace(tag.FirstPerformer) ? null : tag.FirstPerformer,
                AlbumArtist = string.IsNullOrWhiteSpace(tag.FirstAlbumArtist) ? null : tag.FirstAlbumArtist,
                Album = string.IsNullOrWhiteSpace(tag.Album) ? null : tag.Album,
                Year = tag.Year,
                TrackNumber = tag.Track,
                TrackCount = tag.TrackCount,
                Disc = tag.Disc,
                Genre = string.IsNullOrWhiteSpace(tag.FirstGenre) ? null : tag.FirstGenre,
                Confidence = (string.IsNullOrWhiteSpace(tag.Title) || string.IsNullOrWhiteSpace(tag.FirstPerformer)) ? 0.30 : 0.55,
                Source = "id3"
            };
            return meta;
        }
        catch
        {
            return null;
        }
    }

    public static void WriteTags(string path, TrackMetadata m, byte[]? coverArt, bool overwrite)
    {
        try
        {
            using var f = TagLib.File.Create(path);
            var tag = f.Tag;
            if (tag == null) return;

            if (overwrite || string.IsNullOrWhiteSpace(tag.Title))
                tag.Title = m.Title ?? tag.Title;
            if (overwrite || string.IsNullOrWhiteSpace(tag.FirstPerformer))
                tag.Performers = m.Artist != null ? new[] { m.Artist } : tag.Performers;
            if (overwrite || string.IsNullOrWhiteSpace(tag.FirstAlbumArtist))
                tag.AlbumArtists = (m.AlbumArtist ?? m.Artist) != null
                    ? new[] { m.AlbumArtist ?? m.Artist! }
                    : tag.AlbumArtists;
            if (overwrite || string.IsNullOrWhiteSpace(tag.Album))
                tag.Album = m.Album ?? tag.Album;
            if (overwrite || tag.Year == 0)
                if (m.Year > 0) tag.Year = m.Year;
            if (overwrite || tag.Track == 0)
                if (m.TrackNumber > 0) tag.Track = m.TrackNumber;
            if (overwrite || tag.TrackCount == 0)
                if (m.TrackCount > 0) tag.TrackCount = m.TrackCount;
            if (overwrite || string.IsNullOrWhiteSpace(tag.FirstGenre))
                if (!string.IsNullOrWhiteSpace(m.Genre))
                    tag.Genres = new[] { m.Genre! };
            if (!string.IsNullOrEmpty(m.MusicBrainzReleaseId))
                tag.MusicBrainzReleaseId = m.MusicBrainzReleaseId;
            if (!string.IsNullOrEmpty(m.MusicBrainzRecordingId))
                tag.MusicBrainzTrackId = m.MusicBrainzRecordingId;

            if (coverArt is { Length: > 0 } && (overwrite || tag.Pictures.Length == 0))
            {
                var pic = new TagLib.Picture(new TagLib.ByteVector(coverArt))
                {
                    Type = TagLib.PictureType.FrontCover,
                    MimeType = "image/jpeg",
                    Description = "Cover"
                };
                tag.Pictures = new TagLib.IPicture[] { pic };
            }

            f.Save();
        }
        catch
        {
            // Don't fail the whole sort if a single file's tags can't be written.
        }
    }
}
