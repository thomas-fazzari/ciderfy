using Bogus;
using Ciderfy.Apple;
using Ciderfy.Matching;

namespace Ciderfy.Tests.Fakers;

internal static class TrackFakers
{
    private const int DefaultDurationMs = 240_000;

    public static readonly Faker<TrackMetadata> SpotifyTrack = new Faker<TrackMetadata>()
        .RuleFor(t => t.SpotifyId, f => f.Random.AlphaNumeric(22))
        .RuleFor(t => t.Title, f => f.Music.Random.Words(3))
        .RuleFor(t => t.Artist, f => f.Person.FullName)
        .RuleFor(t => t.DurationMs, DefaultDurationMs);

    public static readonly Faker<AppleMusicTrack> AppleTrack = new Faker<AppleMusicTrack>()
        .RuleFor(t => t.Id, f => f.Random.AlphaNumeric(10))
        .RuleFor(t => t.Title, f => f.Music.Random.Words(3))
        .RuleFor(t => t.Artist, f => f.Person.FullName)
        .RuleFor(t => t.DurationMs, DefaultDurationMs);

    public static Faker<TrackMetadata> SpotifyTrackWithDuration(int durationMs) =>
        SpotifyTrack.Clone().RuleFor(t => t.DurationMs, durationMs);

    public static Faker<AppleMusicTrack> AppleTrackWithDuration(int durationMs) =>
        AppleTrack.Clone().RuleFor(t => t.DurationMs, durationMs);
}
