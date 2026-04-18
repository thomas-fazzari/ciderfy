using Ciderfy.Apple;

namespace Ciderfy.Matching;

internal enum MatchMethod
{
    Isrc = 0,
    Text = 1,
}

/// <summary>
/// Outcome of matching a Spotify track to Apple Music
/// </summary>
internal abstract record MatchResult(TrackMetadata SpotifyTrack)
{
    internal sealed record Matched(
        TrackMetadata SpotifyTrack,
        AppleMusicTrack AppleTrack,
        MatchMethod Method,
        double Confidence
    ) : MatchResult(SpotifyTrack);

    internal sealed record NotFound(TrackMetadata SpotifyTrack, string Reason)
        : MatchResult(SpotifyTrack)
    {
        internal const string BelowThresholdPrefix = "Best match below threshold";
    }
}
