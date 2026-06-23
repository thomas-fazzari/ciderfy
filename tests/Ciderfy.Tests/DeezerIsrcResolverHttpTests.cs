using System.Net;
using Ciderfy.Matching;
using Ciderfy.Tests.Fakers;
using Microsoft.Extensions.Options;
using Xunit;

namespace Ciderfy.Tests;

public class DeezerIsrcResolverHttpTests
{
    private static readonly IOptions<DeezerClientOptions> _fastOptions = Options.Create(
        new DeezerClientOptions { RateLimitDelayMs = 1 }
    );

    private static readonly TrackMetadata _track = TrackMetadataFaker.Default.Generate();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static DeezerIsrcResolver Resolver(HttpClient http)
    {
        http.BaseAddress = new Uri(_fastOptions.Value.BaseUrl);
        return new DeezerIsrcResolver(http, _fastOptions);
    }

    [Fact]
    public async Task ResolveIsrcsAsync_SearchesTrackEndpointWithLimit20()
    {
        HttpRequestMessage? capturedRequest = null;
        using var client = new HttpClient(
            new FakeHttpMessageHandler(request =>
            {
                capturedRequest = request;
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("""{ "data": [] }"""),
                };
            })
        );
        using var resolver = Resolver(client);

        await resolver.ResolveIsrcsAsync([_track], ct: Ct);

        Assert.NotNull(capturedRequest);
        Assert.Equal("/search/track", capturedRequest.RequestUri!.AbsolutePath);
        Assert.Contains("limit=20", capturedRequest.RequestUri.Query, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ResolveIsrcsAsync_HttpError_ReturnsTrackWithNullIsrc()
    {
        using var client = FakeHttpMessageHandler.Returning(HttpStatusCode.InternalServerError);
        using var resolver = Resolver(client);

        var results = await resolver.ResolveIsrcsAsync([_track], ct: Ct);

        Assert.Single(results);
        Assert.Null(results[0].Isrc);
    }

    [Fact]
    public async Task ResolveIsrcsAsync_HttpRequestException_ReturnsTrackWithNullIsrc()
    {
        using var client = FakeHttpMessageHandler.ThrowingHttpRequestException();
        using var resolver = Resolver(client);

        var results = await resolver.ResolveIsrcsAsync([_track], ct: Ct);

        Assert.Single(results);
        Assert.Null(results[0].Isrc);
    }

    [Fact]
    public async Task ResolveIsrcsAsync_TimeoutCanceledException_ReturnsTrackWithNullIsrc()
    {
        using var client = FakeHttpMessageHandler.ThrowingTimeoutCanceledException();
        using var resolver = Resolver(client);

        var results = await resolver.ResolveIsrcsAsync([_track], ct: Ct);

        Assert.Single(results);
        Assert.Null(results[0].Isrc);
    }

    [Fact]
    public async Task ResolveIsrcsAsync_EmptyDataArray_ReturnsTrackWithNullIsrc()
    {
        const string json = """{ "data": [] }""";
        using var client = FakeHttpMessageHandler.ReturningJson(json);
        using var resolver = Resolver(client);

        var results = await resolver.ResolveIsrcsAsync([_track], ct: Ct);

        Assert.Single(results);
        Assert.Null(results[0].Isrc);
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("""{ "data": null }""")]
    [InlineData("""{ "data": {} }""")]
    public async Task ResolveIsrcsAsync_MalformedRoot_ReturnsTrackWithNullIsrc(string json)
    {
        using var client = FakeHttpMessageHandler.ReturningJson(json);
        using var resolver = Resolver(client);

        var results = await resolver.ResolveIsrcsAsync([_track], ct: Ct);

        Assert.Single(results);
        Assert.Null(results[0].Isrc);
    }

    [Fact]
    public async Task ResolveIsrcsAsync_MalformedItems_ReturnsTrackWithNullIsrc()
    {
        const string json = """
            {
              "data": [
                null,
                "bad",
                { "isrc": 123, "title": "Let It Be", "artist": { "name": "The Beatles" } },
                { "isrc": "NOMATCH001", "title": [], "artist": null }
              ]
            }
            """;
        using var client = FakeHttpMessageHandler.ReturningJson(json);
        using var resolver = Resolver(client);

        var results = await resolver.ResolveIsrcsAsync([_track], ct: Ct);

        Assert.Single(results);
        Assert.Null(results[0].Isrc);
    }

    [Fact]
    public async Task ResolveIsrcsAsync_ValidResponse_PopulatesIsrc()
    {
        var track = new TrackMetadata
        {
            SpotifyId = "test",
            Title = "Let It Be",
            Artist = "The Beatles",
        };
        const string json = """
            {
              "data": [
                {
                  "isrc": "GBAYE0601498",
                  "title": "Let It Be",
                  "artist": { "name": "The Beatles" }
                }
              ]
            }
            """;
        using var client = FakeHttpMessageHandler.ReturningJson(json);
        using var resolver = Resolver(client);

        var results = await resolver.ResolveIsrcsAsync([track], ct: Ct);

        Assert.Single(results);
        Assert.Equal("GBAYE0601498", results[0].Isrc);
    }

    [Fact]
    public async Task ResolveIsrcsAsync_MultipleResults_ReturnsBestMatch()
    {
        var track = new TrackMetadata
        {
            SpotifyId = "test",
            Title = "Let It Be",
            Artist = "The Beatles",
        };
        const string json = """
            {
              "data": [
                {
                  "isrc": "WRONG001",
                  "title": "Something Else",
                  "artist": { "name": "Other Artist" }
                },
                {
                  "isrc": "GBAYE0601498",
                  "title": "Let It Be",
                  "artist": { "name": "The Beatles" }
                }
              ]
            }
            """;
        using var client = FakeHttpMessageHandler.ReturningJson(json);
        using var resolver = Resolver(client);

        var results = await resolver.ResolveIsrcsAsync([track], ct: Ct);

        Assert.Single(results);
        Assert.Equal("GBAYE0601498", results[0].Isrc);
    }

    [Fact]
    public async Task ResolveIsrcsAsync_StrongVersionMismatch_ReturnsNullIsrc()
    {
        var track = new TrackMetadata
        {
            SpotifyId = "test",
            Title = "Dracula",
            Artist = "Tame Impala",
            DurationMs = 205_000,
        };
        const string json = """
            {
              "data": [
                {
                  "isrc": "USQX92600464",
                  "title": "Dracula (JENNIE Remix)",
                  "title_short": "Dracula",
                  "title_version": "(JENNIE Remix)",
                  "artist": { "name": "Tame Impala" },
                  "duration": 209
                }
              ]
            }
            """;
        using var client = FakeHttpMessageHandler.ReturningJson(json);
        using var resolver = Resolver(client);

        var results = await resolver.ResolveIsrcsAsync([track], ct: Ct);

        Assert.Single(results);
        Assert.Null(results[0].Isrc);
    }

    [Fact]
    public async Task ResolveIsrcsAsync_SourceRemix_SkipsOriginalAndReturnsRemixIsrc()
    {
        var track = new TrackMetadata
        {
            SpotifyId = "test",
            Title = "Dracula (JENNIE Remix)",
            Artist = "Tame Impala",
            DurationMs = 209_000,
        };
        const string json = """
            {
              "data": [
                {
                  "isrc": "USQX92504223",
                  "title": "Dracula",
                  "title_short": "Dracula",
                  "title_version": "",
                  "artist": { "name": "Tame Impala" },
                  "duration": 205
                },
                {
                  "isrc": "USQX92600464",
                  "title": "Dracula (JENNIE Remix)",
                  "title_short": "Dracula",
                  "title_version": "(JENNIE Remix)",
                  "artist": { "name": "Tame Impala" },
                  "duration": 209
                }
              ]
            }
            """;
        using var client = FakeHttpMessageHandler.ReturningJson(json);
        using var resolver = Resolver(client);

        var results = await resolver.ResolveIsrcsAsync([track], ct: Ct);

        Assert.Single(results);
        Assert.Equal("USQX92600464", results[0].Isrc);
    }

    [Fact]
    public async Task ResolveIsrcsAsync_HardDurationMismatch_ReturnsNullIsrc()
    {
        var track = new TrackMetadata
        {
            SpotifyId = "test",
            Title = "Let It Be",
            Artist = "The Beatles",
            DurationMs = 240_000,
        };
        const string json = """
            {
              "data": [
                {
                  "isrc": "GBAYE0601498",
                  "title": "Let It Be",
                  "artist": { "name": "The Beatles" },
                  "duration": 320
                }
              ]
            }
            """;
        using var client = FakeHttpMessageHandler.ReturningJson(json);
        using var resolver = Resolver(client);

        var results = await resolver.ResolveIsrcsAsync([track], ct: Ct);

        Assert.Single(results);
        Assert.Null(results[0].Isrc);
    }

    [Fact]
    public async Task ResolveIsrcsAsync_AmbiguousTopResults_ReturnsCandidatesWithoutConfidentIsrc()
    {
        var track = new TrackMetadata
        {
            SpotifyId = "test",
            Title = "Let It Be",
            Artist = "The Beatles",
            DurationMs = 240_000,
        };
        const string json = """
            {
              "data": [
                {
                  "isrc": "GBAYE0601498",
                  "title": "Let It Be",
                  "artist": { "name": "The Beatles" },
                  "duration": 240
                },
                {
                  "isrc": "GBAYE0601499",
                  "title": "Let It Be",
                  "artist": { "name": "The Beatles" },
                  "duration": 240
                }
              ]
            }
            """;
        using var client = FakeHttpMessageHandler.ReturningJson(json);
        using var resolver = Resolver(client);

        var results = await resolver.ResolveIsrcsAsync([track], ct: Ct);

        Assert.Single(results);
        Assert.Null(results[0].Isrc);
        Assert.Equal(["GBAYE0601498", "GBAYE0601499"], results[0].IsrcCandidates);
    }

    [Fact]
    public async Task ResolveIsrcsAsync_NoBestMatch_ReturnsNullIsrc()
    {
        var track = new TrackMetadata
        {
            SpotifyId = "test",
            Title = "Let It Be",
            Artist = "The Beatles",
        };
        const string json = """
            {
              "data": [
                {
                  "isrc": "NOMATCH001",
                  "title": "Xyz Qwz",
                  "artist": { "name": "Zzz Qqq" }
                }
              ]
            }
            """;
        using var client = FakeHttpMessageHandler.ReturningJson(json);
        using var resolver = Resolver(client);

        var results = await resolver.ResolveIsrcsAsync([track], ct: Ct);

        Assert.Single(results);
        Assert.Null(results[0].Isrc);
    }
}
