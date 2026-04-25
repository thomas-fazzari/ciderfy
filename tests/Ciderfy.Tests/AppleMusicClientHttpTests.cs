using System.Net;
using System.Net.Http.Headers;
using Ciderfy.Apple;
using Ciderfy.Tests.Fakers;
using Microsoft.Extensions.Options;
using Xunit;

namespace Ciderfy.Tests;

public class AppleMusicClientHttpTests
{
    private static readonly IOptions<AppleMusicClientOptions> _fastOptions = Options.Create(
        new AppleMusicClientOptions { MinDelayBetweenCallsMs = 1 }
    );

    private readonly TokenCache _tokenCache = new()
    {
        DeveloperToken = "dev-token",
        DeveloperTokenExpiry = DateTimeOffset.UtcNow.AddHours(1),
        UserToken = "user-token",
        UserTokenExpiry = DateTimeOffset.UtcNow.AddHours(1),
    };

    private AppleMusicClient Client(HttpClient http)
    {
        http.BaseAddress = new Uri(_fastOptions.Value.BaseUrl);
        return new(http, _fastOptions, _tokenCache);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task SendAsync_429_ThrowsRateLimitException()
    {
        using var http = FakeHttpMessageHandler.Returning(HttpStatusCode.TooManyRequests);
        using var client = Client(http);

        await Assert.ThrowsAsync<AppleMusicRateLimitException>(() =>
            client.SearchByTextAllAsync("test", ct: Ct)
        );
    }

    [Fact]
    public async Task SendAsync_429WithRetryAfterDelta_SetsRetrySeconds()
    {
        using var http = FakeHttpMessageHandler.ReturningResponse(
            HttpStatusCode.TooManyRequests,
            r => r.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(30))
        );
        using var client = Client(http);

        var ex = await Assert.ThrowsAsync<AppleMusicRateLimitException>(() =>
            client.SearchByTextAllAsync("test", ct: Ct)
        );

        Assert.Equal(30, ex.RetryAfterSeconds);
    }

    [Fact]
    public async Task SendAsync_401_ThrowsUnauthorizedException()
    {
        using var http = FakeHttpMessageHandler.Returning(HttpStatusCode.Unauthorized);
        using var client = Client(http);

        await Assert.ThrowsAsync<AppleMusicUnauthorizedException>(() =>
            client.SearchByTextAllAsync("test", ct: Ct)
        );
    }

    [Fact]
    public async Task SendAsync_500_ThrowsHttpRequestException()
    {
        using var http = FakeHttpMessageHandler.Returning(
            HttpStatusCode.InternalServerError,
            "internal error"
        );
        using var client = Client(http);

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            client.SearchByTextAllAsync("test", ct: Ct)
        );
    }

    [Fact]
    public async Task SearchByTextAllAsync_NoDeveloperToken_ThrowsUnauthorized()
    {
        using var http = FakeHttpMessageHandler.ReturningJson("{}");
        using var client = new AppleMusicClient(http, _fastOptions, new TokenCache());

        await Assert.ThrowsAsync<AppleMusicUnauthorizedException>(() =>
            client.SearchByTextAllAsync("test", ct: Ct)
        );
    }

    [Fact]
    public async Task CreatePlaylistAsync_NoUserToken_ThrowsInvalidOperation()
    {
        var cache = new TokenCache
        {
            DeveloperToken = "dev-token",
            DeveloperTokenExpiry = DateTimeOffset.UtcNow.AddHours(1),
        };
        using var http = FakeHttpMessageHandler.ReturningJson("{}");
        using var client = new AppleMusicClient(http, _fastOptions, cache);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.CreatePlaylistAsync("Playlist", ct: Ct)
        );
    }

    [Fact]
    public async Task SearchByTextAllAsync_ValidResponse_ReturnsTracks()
    {
        const string json = """
            {
              "results": {
                "songs": {
                  "data": [
                    {
                      "id": "123",
                      "attributes": {
                        "name": "Fortunate Son",
                        "artistName": "Creedence Clearwater Revival",
                        "durationInMillis": 140000,
                        "isrc": "USABC1234567"
                      }
                    }
                  ]
                }
              }
            }
            """;

        using var http = FakeHttpMessageHandler.ReturningJson(json);
        using var client = Client(http);

        var results = await client.SearchByTextAllAsync("Fortunate Son", ct: Ct);

        Assert.Single(results);
        Assert.Equal("123", results[0].Id);
        Assert.Equal("Fortunate Son", results[0].Title);
    }

    [Fact]
    public async Task SearchByTextAllAsync_EmptyData_ReturnsEmpty()
    {
        using var http = FakeHttpMessageHandler.ReturningJson(
            """{ "results": { "songs": { "data": [] } } }"""
        );
        using var client = Client(http);

        var results = await client.SearchByTextAllAsync("test", ct: Ct);

        Assert.Empty(results);
    }

    [Fact]
    public async Task SearchByTextAllAsync_MissingResultsKey_ReturnsEmpty()
    {
        using var http = FakeHttpMessageHandler.ReturningJson("{}");
        using var client = Client(http);

        var results = await client.SearchByTextAllAsync("test", ct: Ct);

        Assert.Empty(results);
    }

    [Fact]
    public async Task BatchSearchByIsrcAsync_EmptyInput_ReturnsEmptyMap()
    {
        using var http = FakeHttpMessageHandler.ReturningJson("{}");
        using var client = Client(http);

        var result = await client.BatchSearchByIsrcAsync([], ct: Ct);

        Assert.Empty(result);
    }

    [Fact]
    public async Task BatchSearchByIsrcAsync_ValidResponse_ReturnsIsrcMap()
    {
        const string json = """
            {
              "data": [
                {
                  "id": "456",
                  "attributes": {
                    "name": "Fortunate Son",
                    "artistName": "CCR",
                    "durationInMillis": 140000,
                    "isrc": "USABC1234567"
                  }
                }
              ]
            }
            """;

        using var http = FakeHttpMessageHandler.ReturningJson(json);
        using var client = Client(http);

        var result = await client.BatchSearchByIsrcAsync(["USABC1234567"], ct: Ct);

        Assert.Single(result);
        Assert.True(result.ContainsKey("USABC1234567"));
        Assert.Equal("456", result["USABC1234567"].Id);
    }

    [Fact]
    public async Task CreatePlaylistAsync_ValidResponse_ReturnsId()
    {
        using var http = FakeHttpMessageHandler.ReturningJson(
            """{ "data": [{ "id": "playlist-abc" }] }"""
        );
        using var client = Client(http);

        var id = await client.CreatePlaylistAsync("My Playlist", ct: Ct);

        Assert.Equal("playlist-abc", id);
    }

    [Fact]
    public async Task CreatePlaylistAsync_EmptyData_ReturnsNull()
    {
        using var http = FakeHttpMessageHandler.ReturningJson("""{ "data": [] }""");
        using var client = Client(http);

        var id = await client.CreatePlaylistAsync("My Playlist", ct: Ct);

        Assert.Null(id);
    }

    [Fact]
    public async Task AddTracksToPlaylistAsync_EmptyList_ReturnsTrue()
    {
        // Empty list short-circuits before any HTTP call
        using var http = FakeHttpMessageHandler.ThrowOnCall();
        using var client = Client(http);

        var result = await client.AddTracksToPlaylistAsync("playlist-id", [], Ct);

        Assert.True(result);
    }

    [Fact]
    public async Task AddTracksToPlaylistAsync_Success_ReturnsTrue()
    {
        using var http = FakeHttpMessageHandler.Returning(HttpStatusCode.NoContent);
        using var client = Client(http);

        var result = await client.AddTracksToPlaylistAsync("playlist-id", ["track1", "track2"], Ct);

        Assert.True(result);
    }
}
