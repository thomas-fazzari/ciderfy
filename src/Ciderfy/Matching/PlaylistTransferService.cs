using Ciderfy.Apple;

namespace Ciderfy.Matching;

/// <summary>
/// Manages the playlist transfers by handling track fetching/matching and playlist creation
/// </summary>
internal sealed class PlaylistTransferService(
    AppleMusicClient appleMusicClient,
    TrackMatcher matcher,
    DeezerIsrcResolver deezerIsrcResolver
)
{
    /// <summary>
    /// Resolves ISRCs via Deezer, then batch-matches against Apple Music
    /// </summary>
    /// <returns>
    /// Matched results and tracks that could not be matched by ISRC
    /// </returns>
    public async Task<(
        List<MatchResult.Matched> Matched,
        List<TrackMetadata> Unmatched
    )> MatchByIsrcAsync(
        IReadOnlyList<TrackMetadata> tracks,
        string storefront,
        IProgress<(int Current, int Total)>? isrcProgress = null,
        CancellationToken ct = default
    )
    {
        // Resolve ISRCs via Deezer
        var enriched = await deezerIsrcResolver
            .ResolveIsrcsAsync(tracks, isrcProgress, ct)
            .ConfigureAwait(false);

        // Partition tracks by ISRC availability
        var matched = new List<MatchResult.Matched>();
        var unmatched = new List<TrackMetadata>();
        var withIsrc = new List<TrackMetadata>();

        foreach (var track in enriched)
        {
            if (GetCandidateIsrcs(track).Count > 0)
            {
                withIsrc.Add(track);
            }
            else
            {
                unmatched.Add(track);
            }
        }

        // Batch ISRC lookup on Apple Music
        var isrcMap =
            withIsrc.Count > 0
                ? await appleMusicClient
                    .BatchSearchByIsrcAsync(
                        withIsrc.SelectMany(GetCandidateIsrcs).Distinct().ToList(),
                        storefront,
                        ct
                    )
                    .ConfigureAwait(false)
                : [];

        foreach (var track in withIsrc)
        {
            var match = FindBestIsrcMatch(track, isrcMap);
            if (match is not null)
            {
                matched.Add(match);
            }
            else
            {
                unmatched.Add(track);
            }
        }

        return (matched, unmatched);
    }

    private static IReadOnlyList<string> GetCandidateIsrcs(TrackMetadata track)
    {
        if (track.IsrcCandidates.Count > 0)
        {
            return track.IsrcCandidates;
        }

        return string.IsNullOrWhiteSpace(track.Isrc) ? [] : [track.Isrc];
    }

    private static MatchResult.Matched? FindBestIsrcMatch(
        TrackMetadata track,
        Dictionary<string, AppleMusicTrack> isrcMap
    )
    {
        MatchResult.Matched? bestMatch = null;

        foreach (var isrc in GetCandidateIsrcs(track))
        {
            if (!isrcMap.TryGetValue(isrc, out var appleTrack))
            {
                continue;
            }

            var resolvedTrack = track with { Isrc = isrc };
            var score = TrackMatcher.CalculateSimilarity(
                resolvedTrack,
                appleTrack,
                allowNamedVersionMismatch: true
            );

            if (score < TrackMatcher.AcceptanceThreshold)
            {
                continue;
            }

            if (bestMatch is null || score > bestMatch.Confidence)
            {
                bestMatch = new MatchResult.Matched(
                    resolvedTrack,
                    appleTrack,
                    MatchMethod.Isrc,
                    score
                );
            }
        }

        return bestMatch;
    }

    /// <summary>
    /// Matches tracks using text-based search
    /// </summary>
    public async Task<List<MatchResult>> MatchByTextAsync(
        IReadOnlyList<TrackMetadata> tracks,
        string storefront,
        IProgress<TrackMatchProgress>? progress = null,
        CancellationToken ct = default
    )
    {
        var results = new MatchResult[tracks.Count];
        var completed = 0;

        await Parallel
            .ForEachAsync(
                Enumerable.Range(0, tracks.Count),
                new ParallelOptions { MaxDegreeOfParallelism = 10, CancellationToken = ct },
                async (i, token) =>
                {
                    var track = tracks[i];
                    results[i] = await matcher
                        .MatchTrackByTextAsync(track, storefront, token)
                        .ConfigureAwait(false);

                    var currentCount = Interlocked.Increment(ref completed);
                    progress?.Report(new TrackMatchProgress(track, currentCount));
                }
            )
            .ConfigureAwait(false);

        return [.. results];
    }

    /// <summary>
    /// Creates an Apple Music playlist and adds the matched tracks to it
    /// </summary>
    /// <returns>
    /// The playlist ID and whether all tracks were added successfully
    /// </returns>
    public async Task<PlaylistCreateResult> CreatePlaylistAsync(
        string name,
        List<MatchResult> matchResults,
        CancellationToken ct = default
    )
    {
        var trackIds = matchResults
            .OfType<MatchResult.Matched>()
            .Select(m => m.AppleTrack.Id)
            .ToList();

        var playlistId = await appleMusicClient
            .CreatePlaylistAsync(name, ct: ct)
            .ConfigureAwait(false);

        if (playlistId is null)
        {
            return new PlaylistCreateResult(null, false);
        }

        var success = await appleMusicClient
            .AddTracksToPlaylistAsync(playlistId, trackIds, ct)
            .ConfigureAwait(false);
        return new PlaylistCreateResult(playlistId, success);
    }
}

internal record TrackMatchProgress(TrackMetadata Track, int CurrentIndex);

internal record PlaylistCreateResult(string? PlaylistId, bool Success);
