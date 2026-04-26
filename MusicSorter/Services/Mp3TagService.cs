using MusicSorter.Models;

namespace MusicSorter.Services;

/// <summary>
/// Reads existing ID3 tags + writes enriched metadata using TagLibSharp.
/// Honors <see cref="TagWriteMode"/> and <see cref="CoverArtMode"/>.
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
                MusicBrainzReleaseId = tag.MusicBrainzReleaseId,
                MusicBrainzReleaseGroupId = tag.MusicBrainzReleaseGroupId,
                MusicBrainzRecordingId = tag.MusicBrainzTrackId,
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

    /// <summary>
    /// Apply the chosen tag and art modes. Both modes are independent — e.g. "strip art"
    /// is fully compatible with "minimal tags".
    /// When <paramref name="confident"/> is false, only Strip modes run — we won't
    /// overwrite real tags with garbage.
    /// </summary>
    public static void WriteTags(string path, TrackMetadata m, byte[]? coverArt,
                                 TagWriteMode tagMode, CoverArtMode artMode, bool confident)
    {
        try
        {
            using var f = TagLib.File.Create(path);
            var tag = f.Tag;
            if (tag == null) return;

            // Strip modes always run — they're destructive on purpose.
            // Other modes only run when we trust the enriched metadata.
            bool tagRan = false;
            if (tagMode == TagWriteMode.Strip)
            {
                ApplyTagMode(tag, m, tagMode);
                tagRan = true;
            }
            else if (confident)
            {
                ApplyTagMode(tag, m, tagMode);
                tagRan = true;
            }

            bool artRan = false;
            if (artMode == CoverArtMode.Strip)
            {
                ApplyArtMode(tag, coverArt, artMode);
                artRan = true;
            }
            else if (confident && artMode == CoverArtMode.EmbedDownloaded)
            {
                ApplyArtMode(tag, coverArt, artMode);
                artRan = true;
            }
            // KeepExisting: never runs — leave pictures alone.

            if (tagRan || artRan)
                f.Save();
        }
        catch
        {
            // Don't fail the whole sort over a single tag write.
        }
    }

    private static void ApplyTagMode(TagLib.Tag tag, TrackMetadata m, TagWriteMode mode)
    {
        switch (mode)
        {
            case TagWriteMode.Strip:
                ClearAll(tag);
                break;

            case TagWriteMode.Minimal:
                ClearAll(tag);
                if (!string.IsNullOrWhiteSpace(m.Title))  tag.Title = m.Title;
                if (!string.IsNullOrWhiteSpace(m.Artist)) tag.Performers = new[] { m.Artist! };
                break;

            case TagWriteMode.FillBlanks:
                Write(tag, m, overwrite: false);
                break;

            case TagWriteMode.Full:
            default:
                Write(tag, m, overwrite: true);
                break;
        }
    }

    private static void ApplyArtMode(TagLib.Tag tag, byte[]? coverArt, CoverArtMode mode)
    {
        switch (mode)
        {
            case CoverArtMode.Strip:
                tag.Pictures = Array.Empty<TagLib.IPicture>();
                break;

            case CoverArtMode.KeepExisting:
                // leave tag.Pictures untouched
                break;

            case CoverArtMode.EmbedDownloaded:
            default:
                if (coverArt is { Length: > 0 })
                {
                    var pic = new TagLib.Picture(new TagLib.ByteVector(coverArt))
                    {
                        Type = TagLib.PictureType.FrontCover,
                        MimeType = SniffMime(coverArt),
                        Description = "Cover"
                    };
                    tag.Pictures = new TagLib.IPicture[] { pic };
                }
                break;
        }
    }

    private static void Write(TagLib.Tag tag, TrackMetadata m, bool overwrite)
    {
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
        if (!string.IsNullOrEmpty(m.MusicBrainzReleaseGroupId))
            tag.MusicBrainzReleaseGroupId = m.MusicBrainzReleaseGroupId;
        if (!string.IsNullOrEmpty(m.MusicBrainzRecordingId))
            tag.MusicBrainzTrackId = m.MusicBrainzRecordingId;
    }

    private static void ClearAll(TagLib.Tag tag)
    {
        tag.Clear();
    }

    /// <summary>Distinguish JPEG vs PNG by magic bytes.</summary>
    private static string SniffMime(byte[] data)
    {
        if (data.Length >= 8 &&
            data[0] == 0x89 && data[1] == 0x50 && data[2] == 0x4E && data[3] == 0x47)
            return "image/png";
        return "image/jpeg";
    }
}
