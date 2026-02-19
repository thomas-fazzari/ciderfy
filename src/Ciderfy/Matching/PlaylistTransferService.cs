using Ciderfy.Apple;
using Ciderfy.Spotify;

namespace Ciderfy.Matching;

/// <summary>
/// Manages the playlist transfers by handling track fetching/matching and playlist creation
/// </summary>
internal sealed class PlaylistTransferService(
    SpotifyClient spotifyClient,
    AppleMusicClient appleMusicClient,
    TrackMatcher matcher,
    DeezerIsrcResolver deezerIsrcResolver
)
{
    /// <summary>
    /// Fetches a Spotify playlist by ID
    /// </summary>
    /// <returns>
    /// The playlist name and tracks
    /// </returns>
    public Task<SpotifyPlaylist> FetchSpotifyPlaylistAsync(
        string playlistId,
        CancellationToken ct = default
    ) => spotifyClient.GetPlaylistAsync(playlistId, ct);

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
        var enriched = await deezerIsrcResolver.ResolveIsrcsAsync(tracks, isrcProgress, ct);

        // Partition tracks by ISRC availability
        var matched = new List<MatchResult.Matched>();
        var unmatched = new List<TrackMetadata>();
        var isrcs = new List<string>();
        var withIsrc = new List<TrackMetadata>();

        foreach (var track in enriched)
        {
            if (!string.IsNullOrEmpty(track.Isrc))
            {
                withIsrc.Add(track);
                isrcs.Add(track.Isrc!);
            }
            else
                unmatched.Add(track);
        }

        // Batch ISRC lookup on Apple Music
        var isrcMap =
            isrcs.Count > 0
                ? await appleMusicClient.BatchSearchByIsrcAsync(isrcs, storefront, ct)
                : [];

        foreach (var track in withIsrc)
        {
            if (isrcMap.TryGetValue(track.Isrc!, out var appleTrack))
                matched.Add(new MatchResult.Matched(track, appleTrack, "ISRC", 1.0));
            else
                unmatched.Add(track);
        }

        return (matched, unmatched);
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

        await Parallel.ForEachAsync(
            Enumerable.Range(0, tracks.Count),
            new ParallelOptions { MaxDegreeOfParallelism = 10, CancellationToken = ct },
            async (i, token) =>
            {
                var track = tracks[i];
                var result = await matcher.MatchTrackByTextAsync(track, storefront, token);
                results[i] = result;

                var currentCount = Interlocked.Increment(ref completed);
                progress?.Report(new TrackMatchProgress(track, currentCount));
            }
        );

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

        var playlistId = await appleMusicClient.CreatePlaylistAsync(name, ct: ct);
        if (playlistId is null)
            return new PlaylistCreateResult(null, false);

        var success = await appleMusicClient.AddTracksToPlaylistAsync(playlistId, trackIds, ct);
        return new PlaylistCreateResult(playlistId, success);
    }
}

internal record TrackMatchProgress(TrackMetadata Track, int CurrentIndex);

internal record PlaylistCreateResult(string? PlaylistId, bool Success);
