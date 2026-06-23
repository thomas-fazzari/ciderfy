using Bogus;
using Ciderfy.Matching;

namespace Ciderfy.Tests.Fakers;

internal static class TrackMetadataFaker
{
    private const int DefaultDurationMs = 240_000;

    public static readonly Faker<TrackMetadata> Default = new Faker<TrackMetadata>()
        .RuleFor(t => t.SpotifyId, f => f.Random.AlphaNumeric(22))
        .RuleFor(t => t.Title, f => f.Music.Random.Words(3))
        .RuleFor(t => t.Artist, f => f.Person.FullName)
        .RuleFor(t => t.Artists, (_, t) => [t.Artist])
        .RuleFor(t => t.DurationMs, DefaultDurationMs);

    public static Faker<TrackMetadata> WithDuration(int durationMs) =>
        Default.Clone().RuleFor(t => t.DurationMs, durationMs);
}
