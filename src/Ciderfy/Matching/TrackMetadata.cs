namespace Ciderfy.Matching;

internal record TrackMetadata
{
    public required string SpotifyId { get; init; }
    public required string Title { get; init; }
    public required string Artist { get; init; }
    public int DurationMs { get; init; }
    public string? Isrc { get; init; }
}
