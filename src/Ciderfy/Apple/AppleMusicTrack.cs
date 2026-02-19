namespace Ciderfy.Apple;

internal record AppleMusicTrack
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public required string Artist { get; init; }
    public int DurationMs { get; init; }
    public string? Isrc { get; init; }
}
