using Bogus;
using Ciderfy.Apple;
using Ciderfy.Matching;

namespace Ciderfy.Tests.Fakers;

internal static class TrackFakers
{
    public static readonly Faker<TrackMetadata> SpotifyTrack = new Faker<TrackMetadata>()
        .RuleFor(t => t.SpotifyId, f => f.Random.AlphaNumeric(22))
        .RuleFor(t => t.Title, f => f.Music.Random.Words(3))
        .RuleFor(t => t.Artist, f => f.Person.FullName)
        .RuleFor(t => t.DurationMs, f => f.Random.Int(120_000, 600_000));

    public static readonly Faker<AppleMusicTrack> AppleTrack = new Faker<AppleMusicTrack>()
        .RuleFor(t => t.Id, f => f.Random.AlphaNumeric(10))
        .RuleFor(t => t.Title, f => f.Music.Random.Words(3))
        .RuleFor(t => t.Artist, f => f.Person.FullName)
        .RuleFor(t => t.DurationMs, f => f.Random.Int(120_000, 600_000));
}
