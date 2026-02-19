using Bogus;
using Ciderfy.Matching;

namespace Ciderfy.Tests.Fakers;

internal static class SpotifyTrackFaker
{
    private const int DefaultDurationMs = 240_000;

    // ISRC (CC-XXX-YY-NNNNN)
    private static string GenerateIsrc(Faker f) =>
        f.Random.String2(2, "ABCDEFGHIJKLMNOPQRSTUVWXYZ") // country
        + f.Random.String2(3, "ABCDEFGHIJKLMNOPQRSTUVWXYZ") // registrant
        + f.Random.String2(2, "0123456789") // year
        + f.Random.String2(5, "0123456789"); // designation

    public static readonly Faker<TrackMetadata> Default = new Faker<TrackMetadata>()
        .RuleFor(t => t.SpotifyId, f => f.Random.AlphaNumeric(22))
        .RuleFor(t => t.Title, f => f.Music.Random.Words(3))
        .RuleFor(t => t.Artist, f => f.Person.FullName)
        .RuleFor(t => t.DurationMs, DefaultDurationMs);

    public static readonly Faker<TrackMetadata> WithIsrc = Default
        .Clone()
        .RuleFor(t => t.Isrc, (f, _) => GenerateIsrc(f));

    public static Faker<TrackMetadata> WithDuration(int durationMs) =>
        Default.Clone().RuleFor(t => t.DurationMs, durationMs);
}
