namespace Ciderfy.Spotify;

internal record SpotifyTrack
{
    public required string SpotifyId { get; init; }
    public required string Title { get; init; }
    public required string Artist { get; init; }
    public IReadOnlyList<string> Artists { get; init; } = [];
    public string? AlbumTitle { get; init; }
    public int DurationMs { get; init; }
}
