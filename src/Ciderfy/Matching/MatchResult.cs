using Ciderfy.Apple;

namespace Ciderfy.Matching;

/// <summary>
/// Outcome of matching a Spotify track to Apple Music
/// </summary>
/// <remarks>
/// Can be either <see cref="Matched"/> (with method and confidence) or <see cref="NotFound"/> (with reason)
/// </remarks>
internal abstract record MatchResult(TrackMetadata SpotifyTrack)
{
    public sealed record Matched(
        TrackMetadata SpotifyTrack,
        AppleMusicTrack AppleTrack,
        string Method,
        double Confidence
    ) : MatchResult(SpotifyTrack);

    public sealed record NotFound(TrackMetadata SpotifyTrack, string Reason)
        : MatchResult(SpotifyTrack);
}
