using Ciderfy.Apple;

namespace Ciderfy.Matching;

/// <summary>
/// Matches Spotify tracks to Apple Music catalog entries using text search with fuzzy similarity scoring
/// </summary>
internal sealed class TrackMatcher(AppleMusicClient appleMusicClient)
{
    // Minimum weighted similarity score (0–1) to accept a text-based match
    internal const double AcceptanceThreshold = 0.7;

    /// <summary>
    /// Attempts to find the best Apple Music match using text-based strategies
    /// </summary>
    public async Task<MatchResult> MatchTrackByTextAsync(
        TrackMetadata spotifyTrack,
        string storefront = "us",
        CancellationToken ct = default
    )
    {
        var cleanTitle = spotifyTrack.Title;
        var normalizedTitle = MusicTextNormalizer.NormalizeTitle(cleanTitle);

        var queries = new List<string>(3) { $"{cleanTitle} {spotifyTrack.Artist}" };

        if (
            normalizedTitle.PrimaryTitle.Length > 0
            && !string.Equals(
                normalizedTitle.PrimaryTitle,
                normalizedTitle.Comparable,
                StringComparison.Ordinal
            )
        )
        {
            queries.Add($"{normalizedTitle.PrimaryTitle} {spotifyTrack.Artist}");
        }

        queries.Add(cleanTitle);

        var seen = new HashSet<string>(queries.Count, StringComparer.Ordinal);

        foreach (var query in queries.Where(seen.Add))
        {
            var match = await TryTextMatchAsync(spotifyTrack, query, storefront, ct)
                .ConfigureAwait(false);
            if (match is not null)
            {
                return match;
            }
        }

        return new MatchResult.NotFound(
            spotifyTrack,
            $"{MatchResult.NotFound.BelowThresholdPrefix} ({AcceptanceThreshold:P0})"
        );
    }

    /// <summary>
    /// Computes a weighted similarity score between a Spotify track and an Apple Music track with title = 60%, artist = 40%,
    /// then applies a multiplier based on the duration for  duration mismatches
    /// </summary>
    internal static double CalculateSimilarity(
        TrackMetadata spotify,
        AppleMusicTrack apple,
        bool allowNamedVersionMismatch = false
    )
    {
        var spotifyTitle = MusicTextNormalizer.NormalizeTitle(spotify.Title);
        var appleTitle = MusicTextNormalizer.NormalizeTitle(apple.Title);

        var versionMultiplier = MusicSimilarity.VersionMultiplier(
            spotifyTitle,
            appleTitle,
            allowNamedVersionMismatch
        );

        if (versionMultiplier <= 0)
        {
            return 0;
        }

        var titleScore = MusicSimilarity.TitleSimilarity(spotifyTitle, appleTitle);
        var artistScore = MusicSimilarity.ArtistSimilarity(spotify.Artist, apple.Artist);
        var textScore =
            (titleScore * MatchingWeights.Title) + (artistScore * MatchingWeights.Artist);

        var albumBonus =
            MusicSimilarity.AlbumSimilarity(spotify.AlbumTitle, apple.AlbumTitle) * 0.03;

        return Math.Min(
            1.0,
            (
                textScore
                * MusicSimilarity.DurationMultiplier(spotify.DurationMs, apple.DurationMs)
                * versionMultiplier
            ) + albumBonus
        );
    }

    private async Task<MatchResult.Matched?> TryTextMatchAsync(
        TrackMetadata spotifyTrack,
        string query,
        string storefront,
        CancellationToken ct
    )
    {
        var candidates = await appleMusicClient
            .SearchByTextAllAsync(query, storefront, ct)
            .ConfigureAwait(false);
        return FindBestMatch(spotifyTrack, candidates);
    }

    internal static MatchResult.Matched? FindBestMatch(
        TrackMetadata spotifyTrack,
        List<AppleMusicTrack> candidates
    )
    {
        AppleMusicTrack? bestCandidate = null;
        var bestScore = 0.0;

        foreach (var candidate in candidates)
        {
            var score = CalculateSimilarity(spotifyTrack, candidate);

            if (!(score > bestScore))
            {
                continue;
            }

            bestScore = score;
            bestCandidate = candidate;
        }

        return bestCandidate is not null && bestScore >= AcceptanceThreshold
            ? new MatchResult.Matched(spotifyTrack, bestCandidate, MatchMethod.Text, bestScore)
            : null;
    }
}
