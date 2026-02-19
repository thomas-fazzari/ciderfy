using Ciderfy.Spotify;

namespace Ciderfy.Matching;

/// <summary>
/// Manage the merging of multiple Spotify playlists into a single deduplicated track list
/// </summary>
internal static class PlaylistMerger
{
    /// <summary>
    /// Merges multiple playlists sequentially, deduplicating tracks by SpotifyId
    /// </summary>
    /// <returns>
    /// Deduplicated tracks in sequential order (playlist 1, then playlist 2, etc.)
    /// </returns>
    internal static List<TrackMetadata> MergeTracks(IReadOnlyList<SpotifyPlaylist> playlists) =>
        [
            .. playlists
                .SelectMany(p => p.Tracks)
                .DistinctBy(t => t.SpotifyId)
                .Select(t => new TrackMetadata
                {
                    SpotifyId = t.SpotifyId,
                    Title = t.Title,
                    Artist = t.Artist,
                    DurationMs = t.DurationMs,
                    Isrc = t.Isrc,
                }),
        ];

    /// <summary>
    /// Resolves the playlist name to use, based on the following priority
    /// </summary>
    /// <remarks>
    /// User-provided name ->
    /// if single playlist: original name ->
    /// if multiple playlists: "Merged Playlist - yyyy-MM-dd"
    /// </remarks>
    internal static string ResolveName(
        IReadOnlyList<SpotifyPlaylist> playlists,
        string? userOverrideName,
        DateOnly? today = null
    )
    {
        if (!string.IsNullOrWhiteSpace(userOverrideName))
            return userOverrideName;

        if (playlists.Count == 1 && !string.IsNullOrWhiteSpace(playlists[0].Name))
            return playlists[0].Name;

        var date = today ?? DateOnly.FromDateTime(DateTime.UtcNow);
        return $"Merged Playlist - {date:yyyy-MM-dd}";
    }
}
