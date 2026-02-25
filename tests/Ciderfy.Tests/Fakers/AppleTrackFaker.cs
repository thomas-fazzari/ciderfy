using Bogus;
using Ciderfy.Apple;

namespace Ciderfy.Tests.Fakers;

internal static class AppleTrackFaker
{
    private const int DefaultDurationMs = 240_000;

    public static readonly Faker<AppleMusicTrack> Default = new Faker<AppleMusicTrack>()
        .RuleFor(t => t.Id, f => f.Random.AlphaNumeric(10))
        .RuleFor(t => t.Title, f => f.Music.Random.Words(3))
        .RuleFor(t => t.Artist, f => f.Person.FullName)
        .RuleFor(t => t.DurationMs, DefaultDurationMs);

    public static Faker<AppleMusicTrack> WithDuration(int durationMs) =>
        Default.Clone().RuleFor(t => t.DurationMs, durationMs);
}
