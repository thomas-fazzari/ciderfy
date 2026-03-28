using Ciderfy.Apple;
using Ciderfy.Configuration.Options;
using Ciderfy.Matching;
using Ciderfy.Tests.Fakers;
using Microsoft.Extensions.Options;
using Xunit;

namespace Ciderfy.Tests;

public class TrackMatcherHttpTests
{
    private static readonly IOptions<AppleMusicClientOptions> _fastOptions = Options.Create(
        new AppleMusicClientOptions { MinDelayBetweenCallsMs = 1 }
    );

    private readonly TokenCache _tokenCache = new()
    {
        DeveloperToken = "dev-token",
        DeveloperTokenExpiry = DateTimeOffset.UtcNow.AddHours(1),
    };

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private TrackMatcher Matcher(HttpClient http) =>
        new(new AppleMusicClient(http, _fastOptions, _tokenCache));

    [Fact]
    public async Task MatchTrackByTextAsync_returns_Matched_when_candidate_found()
    {
        var searchJson = """
            {
              "results": {
                "songs": {
                  "data": [
                    {
                      "id": "am-123",
                      "attributes": {
                        "name": "Fortunate Son",
                        "artistName": "Creedence Clearwater Revival",
                        "durationInMillis": 140000
                      }
                    }
                  ]
                }
              }
            }
            """;

        using var http = FakeHttpMessageHandler.ReturningJson(searchJson);
        var track = SpotifyTrackFaker
            .Default.Clone()
            .RuleFor(t => t.Title, "Fortunate Son")
            .RuleFor(t => t.Artist, "Creedence Clearwater Revival")
            .Generate();

        var result = await Matcher(http).MatchTrackByTextAsync(track, ct: Ct);

        Assert.IsType<MatchResult.Matched>(result);
    }

    [Fact]
    public async Task MatchTrackByTextAsync_returns_NotFound_when_no_candidates_match()
    {
        var emptyJson = """{"results":{"songs":{"data":[]}}}""";

        using var http = FakeHttpMessageHandler.ReturningJson(emptyJson);
        var track = SpotifyTrackFaker
            .Default.Clone()
            .RuleFor(t => t.Title, "Revolution 909")
            .RuleFor(t => t.Artist, "Daft Punk")
            .Generate();

        var result = await Matcher(http).MatchTrackByTextAsync(track, ct: Ct);

        Assert.IsType<MatchResult.NotFound>(result);
    }
}
