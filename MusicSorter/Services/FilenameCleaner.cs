using System.Text.RegularExpressions;
using MusicSorter.Models;

namespace MusicSorter.Services;

/// <summary>
/// Strips YouTube-style cruft from a filename and tries to split it into artist + title.
/// </summary>
public static class FilenameCleaner
{
    private static readonly string[] CruftKeywords =
    {
        "official music video", "official video", "official audio", "official lyric video",
        "official lyrics video", "music video", "lyric video", "lyrics video", "lyrics",
        "lyric", "audio", "video", "hd", "hq", "4k", "1080p", "720p",
        "full album", "full song", "full version", "full hd",
        "extended mix", "extended version", "extended", "radio edit", "radio version",
        "club mix", "original mix", "original",
        "remastered 20\\d\\d", "remastered", "remaster",
        "explicit", "clean", "uncensored", "censored",
        "live", "acoustic", "instrumental", "karaoke",
        "free download", "free dl", "no copyright", "ncs release", "ncs",
        "youtube", "yt", "vevo",
        "prod\\.? by [^\\(\\)\\[\\]]+", "prod\\.? [^\\(\\)\\[\\]]+",
        "official", "visualizer", "visualiser", "visual", "mv"
    };

    private static readonly Regex BracketedCruft = BuildBracketedRegex();
    private static readonly Regex TrailingCruft  = BuildTrailingRegex();
    private static readonly Regex MultiSpace     = new(@"\s+", RegexOptions.Compiled);
    private static readonly Regex SeparatorRun   = new(@"[\s\-_]*[\-_]+[\s\-_]*", RegexOptions.Compiled);
    private static readonly Regex YouTubeId      = new(@"[\s_\-]*[\(\[]?[A-Za-z0-9_-]{11}[\)\]]?\s*$", RegexOptions.Compiled);
    private static readonly Regex LeadingTrack   = new(@"^\s*(\d{1,3})[\.\)\-_\s]+", RegexOptions.Compiled);
    private static readonly Regex FeatPattern    = new(@"\s*[\(\[]?\s*(?:feat\.?|featuring|ft\.?|with)\s+([^\)\]\-]+?)[\)\]]?(?=\s*[-\(\[]|$)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static Regex BuildBracketedRegex()
    {
        var alt = string.Join("|", CruftKeywords);
        // Anything in (...) or [...] that contains one of the keywords.
        var pattern = $@"\s*[\(\[][^\(\)\[\]]*\b(?:{alt})\b[^\(\)\[\]]*[\)\]]";
        return new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);
    }

    private static Regex BuildTrailingRegex()
    {
        var alt = string.Join("|", CruftKeywords);
        // Same keywords but NOT in brackets, anchored near the end of the name.
        var pattern = $@"(?:^|\s|[\-_])\b(?:{alt})\b\s*$";
        return new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);
    }

    public static TrackMetadata CleanFromFileName(string filePath)
    {
        var raw = Path.GetFileNameWithoutExtension(filePath) ?? "";
        var (artist, title) = SplitArtistTitle(Clean(raw));
        return new TrackMetadata
        {
            Artist = artist,
            Title = title,
            Source = "filename",
            Confidence = string.IsNullOrWhiteSpace(artist) ? 0.20 : 0.45
        };
    }

    public static string Clean(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "";

        var s = raw;

        // Replace underscores with spaces (common for YouTube downloads).
        s = s.Replace('_', ' ');

        // Strip trailing 11-char YouTube ID like "...-dQw4w9WgXcQ".
        s = YouTubeId.Replace(s, "");

        // Iterate so nested brackets get cleared.
        for (int i = 0; i < 4; i++)
        {
            var next = BracketedCruft.Replace(s, "");
            if (next == s) break;
            s = next;
        }

        // Then trailing-keyword cleanup ("- HD", "- Lyrics").
        for (int i = 0; i < 3; i++)
        {
            var next = TrailingCruft.Replace(s, "");
            if (next == s) break;
            s = next;
        }

        // Strip empty () or [] blocks left over.
        s = Regex.Replace(s, @"[\(\[]\s*[\)\]]", "");

        // Collapse separator runs and whitespace.
        s = SeparatorRun.Replace(s, " - ");
        s = MultiSpace.Replace(s, " ").Trim(' ', '-', '_', '.', '·');

        // Drop a leading track number like "01 - " (we'll let metadata sources own this).
        s = LeadingTrack.Replace(s, "");

        return s.Trim();
    }

    public static (string? artist, string? title) SplitArtistTitle(string cleaned)
    {
        if (string.IsNullOrWhiteSpace(cleaned)) return (null, null);

        // Pull out (feat. X) so it doesn't confuse the split, but keep it on the title.
        string feat = "";
        var m = FeatPattern.Match(cleaned);
        if (m.Success)
        {
            feat = $" (feat. {m.Groups[1].Value.Trim()})";
            cleaned = FeatPattern.Replace(cleaned, "").Trim();
        }

        // Prefer " - " separator (most common YouTube convention).
        var idx = cleaned.IndexOf(" - ", StringComparison.Ordinal);
        if (idx > 0 && idx < cleaned.Length - 3)
        {
            var artist = cleaned[..idx].Trim().Trim('-', '_').Trim();
            var title  = cleaned[(idx + 3)..].Trim().Trim('-', '_').Trim();
            // If the title still has " - ", keep first chunk only.
            var idx2 = title.IndexOf(" - ", StringComparison.Ordinal);
            if (idx2 > 0) title = title[..idx2].Trim();
            return (NullIfEmpty(artist), NullIfEmpty(title + feat));
        }

        // Fallback: " by " convention ("Song by Artist").
        var byIdx = cleaned.IndexOf(" by ", StringComparison.OrdinalIgnoreCase);
        if (byIdx > 0)
        {
            return (NullIfEmpty(cleaned[(byIdx + 4)..].Trim()),
                    NullIfEmpty(cleaned[..byIdx].Trim() + feat));
        }

        // No separator found — treat the whole thing as the title.
        return (null, NullIfEmpty(cleaned + feat));
    }

    private static string? NullIfEmpty(string? s) => string.IsNullOrWhiteSpace(s) ? null : s;
}
