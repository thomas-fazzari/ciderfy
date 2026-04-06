using Bogus;
using Ciderfy.Spotify;

namespace Ciderfy.Tests.Fakers;

internal static class SpotifyTrackFaker
{
    private const int DefaultDurationMs = 240_000;

    public static readonly Faker<SpotifyTrack> Default = new Faker<SpotifyTrack>()
        .RuleFor(t => t.SpotifyId, f => f.Random.AlphaNumeric(22))
        .RuleFor(t => t.Title, f => f.Music.Random.Words(3))
        .RuleFor(t => t.Artist, f => f.Person.FullName)
        .RuleFor(t => t.DurationMs, DefaultDurationMs);
}
