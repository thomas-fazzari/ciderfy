using Ciderfy.Matching;
using Ciderfy.Tests.Fakers;
using Xunit;

namespace Ciderfy.Tests;

public class MatchResultTests
{
    [Fact]
    public void Matched_StoresAllProperties()
    {
        var spotify = SpotifyTrackFaker.Default.Generate();
        var apple = AppleTrackFaker.Default.Generate();

        var result = new MatchResult.Matched(spotify, apple, MatchMethod.Isrc, 0.95);

        Assert.Equal(spotify, result.SpotifyTrack);
        Assert.Equal(apple, result.AppleTrack);
        Assert.Equal(MatchMethod.Isrc, result.Method);
        Assert.Equal(0.95, result.Confidence);
    }

    [Fact]
    public void NotFound_StoresAllProperties()
    {
        var spotify = SpotifyTrackFaker.Default.Generate();
        const string reason = "no match found";

        var result = new MatchResult.NotFound(spotify, reason);

        Assert.Equal(spotify, result.SpotifyTrack);
        Assert.Equal(reason, result.Reason);
    }
}
