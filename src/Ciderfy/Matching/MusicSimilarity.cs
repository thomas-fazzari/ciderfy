using Raffinert.FuzzySharp;

namespace Ciderfy.Matching;

internal static class MusicSimilarity
{
    private const double SoftVersionMismatchPenalty = 0.96;
    private const double TitleWeight = 0.6;
    private const double ArtistWeight = 0.4;
    private const double AlbumBonusWeight = 0.03;

    private const MusicVersionTag StrongVersionTags =
        MusicVersionTag.Live
        | MusicVersionTag.Remix
        | MusicVersionTag.Acoustic
        | MusicVersionTag.Instrumental
        | MusicVersionTag.Karaoke
        | MusicVersionTag.ReRecorded;

    private const MusicVersionTag SoftVersionTags =
        MusicVersionTag.Remaster
        | MusicVersionTag.Mono
        | MusicVersionTag.Stereo
        | MusicVersionTag.RadioEdit
        | MusicVersionTag.Explicit
        | MusicVersionTag.Clean;

    internal static double TitleSimilarity(string? a, string? b) =>
        TitleSimilarity(
            MusicTextNormalizer.NormalizeTitle(a),
            MusicTextNormalizer.NormalizeTitle(b)
        );

    internal static double TitleSimilarity(NormalizedTitle a, NormalizedTitle b)
    {
        if (a.Comparable.Length == 0 || b.Comparable.Length == 0)
        {
            return 0;
        }

        if (a.Comparable == b.Comparable || a.PrimaryTitle == b.PrimaryTitle)
        {
            return 1.0;
        }

        if (ContainsEither(a.PrimaryTitle, b.PrimaryTitle))
        {
            return 0.90;
        }

        return MaxRatio(
            Fuzz.WeightedRatio(a.PrimaryTitle, b.PrimaryTitle),
            Fuzz.TokenSetRatio(a.PrimaryTitle, b.PrimaryTitle),
            Fuzz.TokenSortRatio(a.PrimaryTitle, b.PrimaryTitle),
            Fuzz.WeightedRatio(a.Comparable, b.Comparable),
            Fuzz.TokenSetRatio(a.Comparable, b.Comparable)
        );
    }

    internal static double ArtistSimilarity(string? a, string? b)
    {
        var normalizedA = MusicTextNormalizer.NormalizeArtist(a);
        var normalizedB = MusicTextNormalizer.NormalizeArtist(b);

        if (normalizedA.Length == 0 || normalizedB.Length == 0)
        {
            return 0;
        }

        if (normalizedA == normalizedB)
        {
            return 1.0;
        }

        if (ContainsEither(normalizedA, normalizedB))
        {
            return 0.90;
        }

        return MaxRatio(
            Fuzz.WeightedRatio(normalizedA, normalizedB),
            Fuzz.TokenSetRatio(normalizedA, normalizedB),
            Fuzz.TokenSortRatio(normalizedA, normalizedB)
        );
    }

    internal static double AlbumSimilarity(string? a, string? b)
    {
        var normalizedA = MusicTextNormalizer.NormalizeAlbum(a);
        var normalizedB = MusicTextNormalizer.NormalizeAlbum(b);

        if (normalizedA.Length == 0 || normalizedB.Length == 0)
        {
            return 0;
        }

        if (normalizedA == normalizedB)
        {
            return 1.0;
        }

        return MaxRatio(
            Fuzz.WeightedRatio(normalizedA, normalizedB),
            Fuzz.TokenSetRatio(normalizedA, normalizedB)
        );
    }

    internal static double VersionMultiplier(
        NormalizedTitle a,
        NormalizedTitle b,
        bool allowNamedVersionMismatch = false
    )
    {
        var diff = a.VersionTags ^ b.VersionTags;

        if ((diff & StrongVersionTags) != MusicVersionTag.None)
        {
            return allowNamedVersionMismatch && (a.HasExtraTitleDetail || b.HasExtraTitleDetail)
                ? SoftVersionMismatchPenalty
                : 0;
        }

        return (diff & SoftVersionTags) == MusicVersionTag.None ? 1.0 : SoftVersionMismatchPenalty;
    }

    internal static double DurationMultiplier(int sourceMs, int candidateMs)
    {
        if (sourceMs <= 0 || candidateMs <= 0)
        {
            return 1.0;
        }

        var diffSeconds = Math.Abs((long)sourceMs - candidateMs) / 1000.0;

        return diffSeconds switch
        {
            <= 5 => 1.0,
            <= 15 => 0.95,
            <= 30 => 0.90,
            <= 60 => 0.80,
            _ => 0.70,
        };
    }

    internal static bool HasHardDurationMismatch(int sourceMs, int candidateMs)
    {
        if (sourceMs <= 0 || candidateMs <= 0)
        {
            return false;
        }

        var diffMs = Math.Abs((long)sourceMs - candidateMs);
        var relativeDiff = diffMs / (double)sourceMs;

        return diffMs > 20_000 && relativeDiff > 0.12;
    }

    internal static double Score(
        double titleScore,
        double artistScore,
        double albumScore,
        double durationMultiplier,
        double versionMultiplier
    ) =>
        Math.Min(
            1.0,
            (
                ((titleScore * TitleWeight) + (artistScore * ArtistWeight))
                * durationMultiplier
                * versionMultiplier
            ) + (albumScore * AlbumBonusWeight)
        );

    private static double MaxRatio(params int[] scores) => scores.Max() / 100.0;

    private static bool ContainsEither(string a, string b) =>
        a.Length >= 4
        && b.Length >= 4
        && (a.Contains(b, StringComparison.Ordinal) || b.Contains(a, StringComparison.Ordinal));
}
