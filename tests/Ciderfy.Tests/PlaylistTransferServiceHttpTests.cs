using System.Net;
using System.Net.Mime;
using System.Text;
using Ciderfy.Apple;
using Ciderfy.Matching;
using Ciderfy.Tests.Fakers;
using Microsoft.Extensions.Options;
using Xunit;

namespace Ciderfy.Tests;

public class PlaylistTransferServiceHttpTests
{
    private const string DeezerNoIsrc = """{"data": []}""";
    private const string DeezerWithIsrc =
        """{"data": [{"isrc": "USABC1234567", "title": "Fortunate Son", "artist": {"name": "Creedence Clearwater Revival"}}]}""";
    private const string AmNoResults = """{"results":{"songs":{"data":[]}}}""";

    private const string AmIsrcTrack = """
        {"data":[{"id":"am-123","attributes":{"name":"Fortunate Son",
        "artistName":"Creedence Clearwater Revival","isrc":"USABC1234567","durationInMillis":140000}}]}
        """;

    private const string AmSearchTrack = """
        {"results":{"songs":{"data":[{"id":"am-456","attributes":
        {"name":"Fortunate Son","artistName":"Creedence Clearwater Revival","durationInMillis":140000}}]}}}
        """;

    private static readonly IOptions<AppleMusicClientOptions> _fastAmOptions = Options.Create(
        new AppleMusicClientOptions { MinDelayBetweenCallsMs = 1 }
    );
    private static readonly IOptions<DeezerClientOptions> _fastDeezerOptions = Options.Create(
        new DeezerClientOptions { RateLimitDelayMs = 1 }
    );

    private readonly TokenCache _tokenCache = new()
    {
        DeveloperToken = "dev-token",
        DeveloperTokenExpiry = DateTimeOffset.UtcNow.AddHours(1),
        UserToken = "user-token",
        UserTokenExpiry = DateTimeOffset.UtcNow.AddHours(1),
    };

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static HttpResponseMessage Ok(string body) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(body, Encoding.UTF8, MediaTypeNames.Application.Json),
        };

    private PlaylistTransferService CreateSut(HttpClient amHttp, HttpClient deezerHttp)
    {
        deezerHttp.BaseAddress = new Uri(DeezerIsrcResolver.BaseUrl);
        amHttp.BaseAddress = new Uri(AppleMusicClient.BaseUrl);
        var amClient = new AppleMusicClient(amHttp, _fastAmOptions, _tokenCache);
        return new PlaylistTransferService(
            amClient,
            new TrackMatcher(amClient),
            new DeezerIsrcResolver(deezerHttp, _fastDeezerOptions)
        );
    }

    private PlaylistTransferService SutWithAmOnly(HttpClient amHttp) =>
        CreateSut(amHttp, FakeHttpMessageHandler.ThrowOnCall());

    [Fact]
    public async Task MatchByIsrcAsync_AllTracksHaveIsrc_ReturnsMatchedResults()
    {
        var track = TrackMetadataFaker
            .Default.Clone()
            .RuleFor(t => t.Title, "Fortunate Son")
            .RuleFor(t => t.Artist, "Creedence Clearwater Revival")
            .RuleFor(t => t.DurationMs, 140_000)
            .Generate();

        using var deezerHttp = FakeHttpMessageHandler.ReturningJson(DeezerWithIsrc);
        using var amHttp = FakeHttpMessageHandler.ReturningJson(AmIsrcTrack);

        var sut = CreateSut(amHttp, deezerHttp);
        var (matched, unmatched) = await sut.MatchByIsrcAsync([track], "us", ct: Ct);

        Assert.Single(matched);
        Assert.Empty(unmatched);
        Assert.Equal("am-123", matched[0].AppleTrack.Id);
        Assert.Equal(MatchMethod.Isrc, matched[0].Method);
        Assert.Equal(1.0, matched[0].Confidence);
    }

    [Fact]
    public async Task MatchByIsrcAsync_DeezerAmbiguousCandidates_AppleValidationChoosesSingle()
    {
        var track = new TrackMetadata
        {
            SpotifyId = "spotify-cruel-intentions",
            Title = "Cruel Intentions",
            Artist = "Simian Mobile Disco",
            AlbumTitle = "Cruel Intentions",
            DurationMs = 185_000,
        };

        const string deezerJson = """
            {
              "data": [
                {
                  "isrc": "GBDNH0900091",
                  "title": "Cruel Intentions",
                  "title_short": "Cruel Intentions",
                  "title_version": "",
                  "artist": { "name": "Simian Mobile Disco" },
                  "album": { "title": "Temporary Pleasure" },
                  "duration": 184
                },
                {
                  "isrc": "GBDNH0900097",
                  "title": "Cruel Intentions (Single Version)",
                  "title_short": "Cruel Intentions",
                  "title_version": "(Single Version)",
                  "artist": { "name": "Simian Mobile Disco" },
                  "album": { "title": "Cruel Intentions" },
                  "duration": 185
                },
                {
                  "isrc": "GBDNH0901161",
                  "title": "Cruel Intentions (Heartbreak's Slow Action Remix)",
                  "title_short": "Cruel Intentions",
                  "title_version": "(Heartbreak's Slow Action Remix)",
                  "artist": { "name": "Simian Mobile Disco" },
                  "album": { "title": "Cruel Intentions" },
                  "duration": 208
                }
              ]
            }
            """;
        const string appleJson = """
            {
              "data": [
                {
                  "id": "am-single",
                  "attributes": {
                    "name": "Cruel Intentions",
                    "artistName": "Simian Mobile Disco",
                    "albumName": "Cruel Intentions",
                    "isrc": "GBDNH0900097",
                    "durationInMillis": 185000
                  }
                }
              ]
            }
            """;

        using var deezerHttp = FakeHttpMessageHandler.ReturningJson(deezerJson);
        using var amHttp = FakeHttpMessageHandler.ReturningJson(appleJson);

        var sut = CreateSut(amHttp, deezerHttp);
        var (matched, unmatched) = await sut.MatchByIsrcAsync([track], "us", ct: Ct);

        var match = Assert.Single(matched);
        Assert.Empty(unmatched);
        Assert.Equal("am-single", match.AppleTrack.Id);
        Assert.Equal("GBDNH0900097", match.SpotifyTrack.Isrc);
    }

    [Fact]
    public async Task MatchByIsrcAsync_NamedCruelIntentionsRemixWithoutRemixWord_MatchesDeezerRemix()
    {
        var track = new TrackMetadata
        {
            SpotifyId = "spotify-cruel-intentions-heartbreak",
            Title = "Cruel Intentions - Heartbreak's Slow Action",
            Artist = "Simian Mobile Disco",
            AlbumTitle = "Cruel Intentions",
            DurationMs = 208_000,
        };

        const string deezerJson = """
            {
              "data": [
                {
                  "isrc": "GBDNH0901161",
                  "title": "Cruel Intentions (Heartbreak's Slow Action Remix)",
                  "title_short": "Cruel Intentions",
                  "title_version": "(Heartbreak's Slow Action Remix)",
                  "artist": { "name": "Simian Mobile Disco" },
                  "album": { "title": "Cruel Intentions" },
                  "duration": 208
                },
                {
                  "isrc": "GBDNH0900091",
                  "title": "Cruel Intentions",
                  "title_short": "Cruel Intentions",
                  "title_version": "",
                  "artist": { "name": "Simian Mobile Disco" },
                  "album": { "title": "Temporary Pleasure" },
                  "duration": 184
                }
              ]
            }
            """;
        const string appleJson = """
            {
              "data": [
                {
                  "id": "am-heartbreak",
                  "attributes": {
                    "name": "Cruel Intentions (Heartbreak's Slow Action Remix)",
                    "artistName": "Simian Mobile Disco",
                    "albumName": "Cruel Intentions",
                    "isrc": "GBDNH0901161",
                    "durationInMillis": 208000
                  }
                }
              ]
            }
            """;

        using var deezerHttp = FakeHttpMessageHandler.ReturningJson(deezerJson);
        using var amHttp = FakeHttpMessageHandler.ReturningJson(appleJson);

        var sut = CreateSut(amHttp, deezerHttp);
        var (matched, unmatched) = await sut.MatchByIsrcAsync([track], "us", ct: Ct);

        var match = Assert.Single(matched);
        Assert.Empty(unmatched);
        Assert.Equal("am-heartbreak", match.AppleTrack.Id);
        Assert.Equal("GBDNH0901161", match.SpotifyTrack.Isrc);
    }

    [Fact]
    public async Task MatchByIsrcAsync_DeezerReturnsNoIsrc_TrackIsUnmatched_AmNotCalled()
    {
        using var deezerHttp = FakeHttpMessageHandler.ReturningJson(DeezerNoIsrc);
        using var amHttp = FakeHttpMessageHandler.ThrowOnCall();

        var sut = CreateSut(amHttp, deezerHttp);
        var (matched, unmatched) = await sut.MatchByIsrcAsync(
            [TrackMetadataFaker.Default.Generate()],
            "us",
            ct: Ct
        );

        Assert.Empty(matched);
        Assert.Single(unmatched);
    }

    [Fact]
    public async Task MatchByTextAsync_CandidateFound_ReturnsMatched()
    {
        var track = TrackMetadataFaker
            .Default.Clone()
            .RuleFor(t => t.Title, "Fortunate Son")
            .RuleFor(t => t.Artist, "Creedence Clearwater Revival")
            .RuleFor(t => t.DurationMs, 140_000)
            .Generate();

        using var amHttp = FakeHttpMessageHandler.ReturningJson(AmSearchTrack);

        var results = await SutWithAmOnly(amHttp).MatchByTextAsync([track], "us", ct: Ct);

        var matched = Assert.IsType<MatchResult.Matched>(Assert.Single(results));
        Assert.Equal("am-456", matched.AppleTrack.Id);
        Assert.Equal(MatchMethod.Text, matched.Method);
    }

    [Fact]
    public async Task MatchByTextAsync_NoCandidates_ReturnsNotFound()
    {
        using var amHttp = FakeHttpMessageHandler.ReturningJson(AmNoResults);

        var results = await SutWithAmOnly(amHttp)
            .MatchByTextAsync([TrackMetadataFaker.Default.Generate()], "us", ct: Ct);

        Assert.IsType<MatchResult.NotFound>(Assert.Single(results));
    }

    [Fact]
    public async Task CreatePlaylistAsync_NoPlaylistId_ReturnsNullFalse()
    {
        using var amHttp = FakeHttpMessageHandler.ReturningJson("""{"data": []}""");

        var result = await SutWithAmOnly(amHttp)
            .CreatePlaylistAsync(
                "My Playlist",
                [
                    new MatchResult.Matched(
                        TrackMetadataFaker.Default.Generate(),
                        AppleTrackFaker.Default.Generate(),
                        MatchMethod.Isrc,
                        1.0
                    ),
                ],
                Ct
            );

        Assert.Null(result.PlaylistId);
        Assert.False(result.Success);
    }

    [Fact]
    public async Task CreatePlaylistAsync_Success_ReturnsPlaylistIdAndTrue()
    {
        var matched = new MatchResult.Matched(
            TrackMetadataFaker.Default.Generate(),
            AppleTrackFaker.Default.Clone().RuleFor(t => t.Id, "am-track-1").Generate(),
            MatchMethod.Isrc,
            1.0
        );

        using var amHttp = new HttpClient(
            new FakeHttpMessageHandler(request =>
            {
                var path = request.RequestUri!.AbsolutePath;

                if (path.EndsWith("/playlists", StringComparison.Ordinal))
                {
                    return Ok("""{"data": [{"id": "pl-789"}]}""");
                }

                return path.EndsWith("/tracks", StringComparison.Ordinal)
                    ? Ok("{}")
                    : throw new InvalidOperationException($"Unexpected AM path: {path}");
            })
        );

        var result = await SutWithAmOnly(amHttp).CreatePlaylistAsync("My Playlist", [matched], Ct);

        Assert.Equal("pl-789", result.PlaylistId);
        Assert.True(result.Success);
    }
}
