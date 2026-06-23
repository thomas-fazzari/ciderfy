using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Ciderfy.Matching;

internal readonly record struct NormalizedTitle(
    string Comparable,
    string PrimaryTitle,
    bool HasExtraTitleDetail,
    MusicVersionTag VersionTags
);

internal static partial class MusicTextNormalizer
{
    internal static NormalizedTitle NormalizeTitle(
        string? title,
        string? versionText = null,
        MusicVersionTag additionalTags = MusicVersionTag.None
    )
    {
        var normalized = NormalizeSymbols(title);
        var versionSource = string.IsNullOrWhiteSpace(versionText)
            ? normalized
            : $"{normalized} {NormalizeSymbols(versionText)}";
        var tags = ExtractVersionTags(versionSource) | additionalTags;

        var comparableSource = FeatureClauseRegex().Replace(normalized, " ");
        comparableSource = ParentheticalWithRegex().Replace(comparableSource, " ");
        comparableSource = VersionBlockRegex().Replace(comparableSource, " ");
        comparableSource = TrailingVersionRegex().Replace(comparableSource, " ");

        var comparable = NormalizeTokens(comparableSource);
        var primary = NormalizeTokens(PrimarySeparatorRegex().Split(comparableSource, 2)[0]);

        if (primary.Length == 0)
        {
            primary = comparable;
        }

        return new NormalizedTitle(comparable, primary, comparable.Length > primary.Length, tags);
    }

    internal static string NormalizeArtist(string? artist)
    {
        var normalized = NormalizeTokens(NormalizeSymbols(artist));

        return normalized.StartsWith("the ", StringComparison.Ordinal)
            ? normalized[4..]
            : normalized;
    }

    internal static string NormalizeAlbum(string? album) => NormalizeTitle(album).Comparable;

    private static MusicVersionTag ExtractVersionTags(string value)
    {
        var tags = MusicVersionTag.None;

        if (LiveRegex().IsMatch(value))
        {
            tags |= MusicVersionTag.Live;
        }

        if (RemixRegex().IsMatch(value))
        {
            tags |= MusicVersionTag.Remix;
        }

        if (RemasterRegex().IsMatch(value))
        {
            tags |= MusicVersionTag.Remaster;
        }

        if (MonoRegex().IsMatch(value))
        {
            tags |= MusicVersionTag.Mono;
        }

        if (StereoRegex().IsMatch(value))
        {
            tags |= MusicVersionTag.Stereo;
        }

        if (RadioEditRegex().IsMatch(value))
        {
            tags |= MusicVersionTag.RadioEdit;
        }

        if (AcousticRegex().IsMatch(value))
        {
            tags |= MusicVersionTag.Acoustic;
        }

        if (InstrumentalRegex().IsMatch(value))
        {
            tags |= MusicVersionTag.Instrumental;
        }

        if (KaraokeRegex().IsMatch(value))
        {
            tags |= MusicVersionTag.Karaoke;
        }

        if (ExplicitRegex().IsMatch(value))
        {
            tags |= MusicVersionTag.Explicit;
        }

        if (CleanRegex().IsMatch(value))
        {
            tags |= MusicVersionTag.Clean;
        }

        if (ReRecordedRegex().IsMatch(value))
        {
            tags |= MusicVersionTag.ReRecorded;
        }

        return tags;
    }

    private static string NormalizeSymbols(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var decomposed = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);

        foreach (var c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            builder.Append(
                c switch
                {
                    '\u2018' or '\u2019' or '\u201A' or '\u201B' or '\u2032' => '\'',
                    '\u201C' or '\u201D' or '\u201E' or '\u201F' or '\u2033' => '"',
                    '\u2010' or '\u2011' or '\u2012' or '\u2013' or '\u2014' or '\u2212' => '-',
                    _ => char.ToLowerInvariant(c),
                }
            );
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }

    private static string NormalizeTokens(string value)
    {
        value = value
            .Replace("&", " and ", StringComparison.Ordinal)
            .Replace("'", string.Empty, StringComparison.Ordinal);
        value = NonTokenRegex().Replace(value, " ");
        return SpaceRegex().Replace(value, " ").Trim();
    }

    [GeneratedRegex(
        @"(?ix)(?:\s+|[\(\[\-\/])(?:feat\.?|ft\.?|featuring)\s+.*$",
        RegexOptions.None,
        1000
    )]
    private static partial Regex FeatureClauseRegex();

    [GeneratedRegex(@"(?ix)[\(\[]\s*with\s+[^)\]]+[\)\]]", RegexOptions.None, 1000)]
    private static partial Regex ParentheticalWithRegex();

    [GeneratedRegex(
        @"(?ix)\s*[\(\[][^)\]]*\b(live|remix|(?<!original\s)mix|dub|a\s*cappella|remaster(?:ed)?|mono|stereo|radio\s*edit|acoustic|instrumental|karaoke|explicit|clean|single\s+version|deluxe\s+edition|bonus\s+track|original\s+mix|re-recorded)\b[^)\]]*[\)\]]",
        RegexOptions.None,
        1000
    )]
    private static partial Regex VersionBlockRegex();

    [GeneratedRegex(
        @"(?ix)\s+(?:-|/)\s+.*\b(live|remix|(?<!original\s)mix|dub|a\s*cappella|remaster(?:ed)?|mono|stereo|radio\s*edit|acoustic|instrumental|karaoke|explicit|clean|single\s+version|deluxe\s+edition|bonus\s+track|original\s+mix|re-recorded)\b.*$",
        RegexOptions.None,
        1000
    )]
    private static partial Regex TrailingVersionRegex();

    [GeneratedRegex(@"\s+(?:/|-)\s+", RegexOptions.None, 1000)]
    private static partial Regex PrimarySeparatorRegex();

    [GeneratedRegex(@"[^a-z0-9]+", RegexOptions.None, 1000)]
    private static partial Regex NonTokenRegex();

    [GeneratedRegex(@"\s+", RegexOptions.None, 1000)]
    private static partial Regex SpaceRegex();

    [GeneratedRegex(@"\blive\b", RegexOptions.None, 1000)]
    private static partial Regex LiveRegex();

    [GeneratedRegex(@"\b(remix|(?<!original\s)mix|dub|a\s*cappella)\b", RegexOptions.None, 1000)]
    private static partial Regex RemixRegex();

    [GeneratedRegex(@"\bremaster(?:ed)?\b", RegexOptions.None, 1000)]
    private static partial Regex RemasterRegex();

    [GeneratedRegex(@"\bmono\b", RegexOptions.None, 1000)]
    private static partial Regex MonoRegex();

    [GeneratedRegex(@"\bstereo\b", RegexOptions.None, 1000)]
    private static partial Regex StereoRegex();

    [GeneratedRegex(@"\b(?:radio\s*edit|single\s+version)\b", RegexOptions.None, 1000)]
    private static partial Regex RadioEditRegex();

    [GeneratedRegex(@"\bacoustic\b", RegexOptions.None, 1000)]
    private static partial Regex AcousticRegex();

    [GeneratedRegex(@"\binstrumental\b", RegexOptions.None, 1000)]
    private static partial Regex InstrumentalRegex();

    [GeneratedRegex(@"\bkaraoke\b", RegexOptions.None, 1000)]
    private static partial Regex KaraokeRegex();

    [GeneratedRegex(@"\bexplicit\b", RegexOptions.None, 1000)]
    private static partial Regex ExplicitRegex();

    [GeneratedRegex(@"\bclean\b", RegexOptions.None, 1000)]
    private static partial Regex CleanRegex();

    [GeneratedRegex(@"\bre-recorded\b", RegexOptions.None, 1000)]
    private static partial Regex ReRecordedRegex();
}
