using System.Text.RegularExpressions;
using Ciderfy.Apple;
using F23.StringSimilarity;

namespace Ciderfy.Matching;

/// <summary>
/// Matches Spotify tracks to Apple Music catalog entries using text search with fuzzy similarity scoring
/// </summary>
internal sealed partial class TrackMatcher(AppleMusicClient appleMusicClient)
{
    // Minimum weighted similarity score (0–1) to accept a text-based match
    private const double AcceptanceThreshold = 0.7;

    private static readonly JaroWinkler _jaroWinkler = new();

    /// <summary>
    /// Attempts to find the best Apple Music match using text-based strategies
    /// </summary>
    public async Task<MatchResult> MatchTrackByTextAsync(
        TrackMetadata spotifyTrack,
        string storefront = "us",
        CancellationToken ct = default
    )
    {
        var cleanTitle = StripVersionSuffix(spotifyTrack.Title);
        var primaryTitle = ExtractPrimaryTitle(NormalizeForComparison(cleanTitle));
        var normalizedClean = NormalizeForComparison(cleanTitle);

        var queries = new List<string>(3) { $"{cleanTitle} {spotifyTrack.Artist}" };

        if (!string.Equals(primaryTitle, normalizedClean, StringComparison.Ordinal))
            queries.Add($"{primaryTitle} {spotifyTrack.Artist}");

        queries.Add(cleanTitle);

        var seen = new HashSet<string>(queries.Count, StringComparer.Ordinal);
        foreach (var query in queries)
        {
            if (!seen.Add(query))
                continue;

            var match = await TryTextMatchAsync(spotifyTrack, query, storefront, ct);
            if (match is not null)
                return match;
        }

        return new MatchResult.NotFound(
            spotifyTrack,
            $"Best match below threshold ({AcceptanceThreshold:P0})"
        );
    }

    /// <summary>
    /// Computes a weighted similarity score between a Spotify track and an Apple Music track with title = 60%, artist = 40%,
    /// then applies a multiplier based on the duration for  duration mismatches
    /// </summary>
    internal static double CalculateSimilarity(TrackMetadata spotify, AppleMusicTrack apple)
    {
        var titleScore = TitleSimilarity(spotify.Title, apple.Title);
        var artistScore = ArtistSimilarity(spotify.Artist, apple.Artist);
        var textScore = (titleScore * 0.6) + (artistScore * 0.4);

        return textScore * DurationMultiplier(spotify.DurationMs, apple.DurationMs);
    }

    /// <summary>
    /// Returns a multiplier between 0.7 and 1 based on how close two track durations are
    /// </summary>
    internal static double DurationMultiplier(int spotifyMs, int appleMs)
    {
        if (spotifyMs <= 0 || appleMs <= 0)
            return 1.0;

        var diffSeconds = Math.Abs(spotifyMs - appleMs) / 1000.0;

        return diffSeconds switch
        {
            <= 5 => 1.0,
            <= 15 => 0.95,
            <= 30 => 0.90,
            <= 60 => 0.80,
            _ => 0.70,
        };
    }

    /// <summary>
    /// Compares two cleaned title strings
    /// </summary>
    internal static double TitleSimilarity(string a, string b)
    {
        if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b))
            return 0;

        var normalA = NormalizeForComparison(a);
        var normalB = NormalizeForComparison(b);

        if (QuickSimilarity(normalA, normalB) is { } score)
            return score;

        // Compare primary title segments
        var primaryA = ExtractPrimaryTitle(normalA);
        var primaryB = ExtractPrimaryTitle(normalB);

        if (primaryA == primaryB)
            return 0.95;

        if (ContainsEither(primaryA, primaryB))
            return 0.85;

        // Take the best between primary segment and Jaro-Winkler
        return Math.Max(
            _jaroWinkler.Similarity(primaryA, primaryB),
            _jaroWinkler.Similarity(normalA, normalB)
        );
    }

    /// <summary>
    /// Compares two normalized artist strings
    /// </summary>
    internal static double ArtistSimilarity(string a, string b)
    {
        if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b))
            return 0;

        var normalA = NormalizeArtistForComparison(a);
        var normalB = NormalizeArtistForComparison(b);

        return QuickSimilarity(normalA, normalB) ?? _jaroWinkler.Similarity(normalA, normalB);
    }

    /// <summary>
    /// Strips version suffixes and parenthesized version info from a title
    /// </summary>
    /// <remarks>
    /// Ex: "Suzie Q (Remastered 2014)" → "Suzie Q"
    /// </remarks>
    internal static string StripVersionSuffix(string s)
    {
        s = ParenVersionRegex().Replace(s, "");
        s = VersionSuffixRegex().Replace(s, "");
        return s.Trim();
    }

    /// <summary>
    /// Computes full normalization for fuzzy comparison
    /// </summary>
    internal static string NormalizeForComparison(string s)
    {
        s = StripVersionSuffix(s);
        s = s.ToLowerInvariant();

        // Normalize en-dash and em-dash to hyphen
        s = s.Replace('\u2013', '-').Replace('\u2014', '-');

        // Remove apostrophes
        s = s.Replace("'", "").Replace("\u2019", "");

        // Remove quotes, parentheses, brackets
        s = s.Replace("\"", "").Replace("(", "").Replace(")", "").Replace("[", "").Replace("]", "");

        // Remove featuring clauses
        s = FeaturingRegex().Replace(s, "");

        // Normalize "&" to "and"
        s = s.Replace(" & ", " and ");

        return s.Trim();
    }

    private async Task<MatchResult.Matched?> TryTextMatchAsync(
        TrackMetadata spotifyTrack,
        string query,
        string storefront,
        CancellationToken ct
    )
    {
        var candidates = await appleMusicClient.SearchByTextAllAsync(query, storefront, ct);
        return FindBestMatch(spotifyTrack, candidates);
    }

    private static MatchResult.Matched? FindBestMatch(
        TrackMetadata spotifyTrack,
        List<AppleMusicTrack> candidates
    )
    {
        AppleMusicTrack? bestCandidate = null;
        var bestScore = 0.0;

        foreach (var candidate in candidates)
        {
            var score = CalculateSimilarity(spotifyTrack, candidate);
            if (score > bestScore)
            {
                bestScore = score;
                bestCandidate = candidate;
            }
        }

        return bestCandidate is not null && bestScore >= AcceptanceThreshold
            ? new MatchResult.Matched(spotifyTrack, bestCandidate, "text", bestScore)
            : null;
    }

    private static double? QuickSimilarity(string a, string b)
    {
        if (a == b)
            return 1.0;
        if (ContainsEither(a, b))
            return 0.9;
        return null;
    }

    private static bool ContainsEither(string a, string b) =>
        a.Contains(b, StringComparison.Ordinal) || b.Contains(a, StringComparison.Ordinal);

    /// <summary>
    /// Normalizes an artist name
    /// </summary>
    private static string NormalizeArtistForComparison(string s)
    {
        var normalized = NormalizeForComparison(s);

        if (normalized.StartsWith("the ", StringComparison.Ordinal))
            normalized = normalized[4..];

        return normalized;
    }

    /// <summary>
    /// Extracts the primary title segment before structural separators like " / " or " - "
    /// </summary>
    /// <remarks>
    /// Ex: "War Pigs / Luke's Wall" → "War Pigs"
    /// </remarks>
    private static string ExtractPrimaryTitle(string normalized)
    {
        var slashIdx = normalized.IndexOf(" / ", StringComparison.Ordinal);
        if (slashIdx > 0)
            return normalized[..slashIdx].Trim();

        var dashIdx = normalized.IndexOf(" - ", StringComparison.Ordinal);
        if (dashIdx > 0)
            return normalized[..dashIdx].Trim();

        return normalized;
    }

    // Matches version suffixes after a dash or slash separator, ex: "- 2024 Remaster"
    [GeneratedRegex(
        @"\s*[-/\u2013\u2014]\s*(remaster(ed)?(\s+\d{4})?|(\d{4}\s+)?remaster"
            + @"|stereo(\s+version)?|mono(\s+(single\s+)?version)?"
            + @"|single\s+version|deluxe(\s+edition)?"
            + @"|original(\s+mix)?|live(\s+(at|version)\b)?"
            + @"|bonus\s+track|remix"
            + @"|re-recorded"
            + @").*$",
        RegexOptions.IgnoreCase
    )]
    private static partial Regex VersionSuffixRegex();

    // Matches featuring clauses in titles
    [GeneratedRegex(@"\s*(feat\.?|ft\.?)\s+.*$", RegexOptions.IgnoreCase)]
    private static partial Regex FeaturingRegex();

    // Matches parenthesized/bracketed version info, ex: "(Remastered 2012)"
    [GeneratedRegex(
        @"\s*[\(\[](remaster(ed)?(\s+\d{4})?|(\d{4}\s+)?remaster"
            + @"|stereo(\s+version)?|mono(\s+(single\s+)?version)?"
            + @"|single\s+version|deluxe(\s+edition)?"
            + @"|original(\s+(mix|stereo|mono))?|live(\s+(at|version)\b)?"
            + @"|bonus\s+track|remix"
            + @"|re-recorded"
            + @"|feat\.?\s+.+|ft\.?\s+.+"
            + @")[\)\]]",
        RegexOptions.IgnoreCase
    )]
    private static partial Regex ParenVersionRegex();
}
