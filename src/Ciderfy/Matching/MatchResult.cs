using Ciderfy.Apple;

namespace Ciderfy.Matching;

internal enum MatchMethod
{
    Isrc,
    Text,
}

/// <summary>
/// Outcome of matching a Spotify track to Apple Music
/// </summary>
internal abstract record MatchResult(TrackMetadata SpotifyTrack)
{
    public sealed record Matched(
        TrackMetadata SpotifyTrack,
        AppleMusicTrack AppleTrack,
        MatchMethod Method,
        double Confidence
    ) : MatchResult(SpotifyTrack);

    public sealed record NotFound(TrackMetadata SpotifyTrack, string Reason)
        : MatchResult(SpotifyTrack)
    {
        internal const string BelowThresholdPrefix = "Best match below threshold";
    }
}
