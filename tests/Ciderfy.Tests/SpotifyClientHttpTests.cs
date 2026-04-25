using System.Net;
using System.Text;
using Ciderfy.Spotify;
using Ciderfy.Tests.Fakers;
using Xunit;

namespace Ciderfy.Tests;

public class SpotifyClientHttpTests
{
    private const string SpotifyHost = "open.spotify.com";
    private const string ClientTokenHost = "clienttoken.spotify.com";
    private const string AccessTokenJson = """{"accessToken":"tok","clientId":"cid"}""";
    private const string ClientTokenJson = """{"granted_token":{"token":"ctok"}}""";
    private static readonly string SessionHtml = BuildSessionHtml();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static SpotifyClient Client(HttpClient http) => new(http, new CookieContainer());

    private static string BuildSessionHtml()
    {
        var base64 = Convert.ToBase64String(
            Encoding.UTF8.GetBytes("""{"clientVersion":"1.2.3"}""")
        );
        return $"""<script id="appServerConfig" type="text/plain">{base64}</script>""";
    }

    [Fact]
    public async Task GetPlaylistAsync_NetworkFailure_ThrowsHttpRequestException()
    {
        using var http = FakeHttpMessageHandler.ThrowingHttpRequestException();

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            Client(http).GetPlaylistAsync("anyid", Ct)
        );
    }

    [Fact]
    public async Task GetPlaylistAsync_Timeout_ThrowsTaskCanceledException()
    {
        using var http = FakeHttpMessageHandler.ThrowingTimeoutCanceledException();

        await Assert.ThrowsAsync<TaskCanceledException>(() =>
            Client(http).GetPlaylistAsync("anyid", Ct)
        );
    }

    [Fact]
    public async Task GetPlaylistAsync_ValidResponse_ReturnsPlaylistWithTracks()
    {
        const string playlistName = "My Playlist";
        const string trackTitle = "Track One";
        const string artistName = "Artist A";
        const string trackId = "abc123";
        const int durationMs = 210000;

        var playlistJson = $$"""
            {
              "data": {
                "playlistV2": {
                  "name": "{{playlistName}}",
                  "content": {
                    "items": [
                      {
                        "itemV2": {
                          "data": {
                            "name": "{{trackTitle}}",
                            "uri": "spotify:track:{{trackId}}",
                            "artists": { "items": [{ "profile": { "name": "{{artistName}}" } }] },
                            "trackDuration": { "totalMilliseconds": {{durationMs}} }
                          }
                        }
                      }
                    ]
                  }
                }
              }
            }
            """;

        using var http = new HttpClient(
            new FakeHttpMessageHandler(request =>
            {
                static HttpResponseMessage Ok(string body) =>
                    new(HttpStatusCode.OK) { Content = new StringContent(body) };

                var uri = request.RequestUri!;
                if (uri.Host == SpotifyHost && uri.AbsolutePath == "/")
                    return Ok(SessionHtml);
                if (
                    uri.Host == SpotifyHost
                    && uri.AbsolutePath.StartsWith("/api/token", StringComparison.Ordinal)
                )
                {
                    return Ok(AccessTokenJson);
                }
                if (uri.Host == ClientTokenHost)
                    return Ok(ClientTokenJson);
                return Ok(playlistJson);
            })
        );

        var playlist = await Client(http).GetPlaylistAsync("playlist123", Ct);

        Assert.Equal(playlistName, playlist.Name);
        Assert.Single(playlist.Tracks);
        Assert.Equal(trackTitle, playlist.Tracks[0].Title);
        Assert.Equal(artistName, playlist.Tracks[0].Artist);
        Assert.Equal(trackId, playlist.Tracks[0].SpotifyId);
        Assert.Equal(durationMs, playlist.Tracks[0].DurationMs);
    }

    [Fact]
    public async Task GetPlaylistAsync_UnauthorizedMidSession_InvalidatesStateAndRetries()
    {
        const string playlistName = "Refreshed Playlist";

        var playlistJson = $$"""
            {
              "data": {
                "playlistV2": {
                  "name": "{{playlistName}}",
                  "content": { "items": [] }
                }
              }
            }
            """;

        var playlistCallCount = 0;
        var accessTokenCallCount = 0;

        using var http = new HttpClient(
            new FakeHttpMessageHandler(request =>
            {
                static HttpResponseMessage Ok(string body) =>
                    new(HttpStatusCode.OK) { Content = new StringContent(body) };

                var uri = request.RequestUri!;
                if (uri.Host == SpotifyHost && uri.AbsolutePath == "/")
                    return Ok(SessionHtml);
                if (
                    uri.Host == SpotifyHost
                    && uri.AbsolutePath.StartsWith("/api/token", StringComparison.Ordinal)
                )
                {
                    accessTokenCallCount++;
                    return Ok(AccessTokenJson);
                }
                if (uri.Host == ClientTokenHost)
                    return Ok(ClientTokenJson);

                // First playlist call returns 401 (stale token), second succeeds
                playlistCallCount++;
                if (playlistCallCount == 1)
                    return new HttpResponseMessage(HttpStatusCode.Unauthorized);

                return Ok(playlistJson);
            })
        );

        var playlist = await Client(http).GetPlaylistAsync("playlist123", Ct);

        Assert.Equal(playlistName, playlist.Name);
        Assert.Equal(2, playlistCallCount);
        Assert.Equal(2, accessTokenCallCount);
    }

    [Fact]
    public async Task GetPlaylistAsync_ClientTokenFailure_RetriesAuthOnNextCall()
    {
        const string playlistName = "Recovered Playlist";

        var playlistJson = $$"""
            {
              "data": {
                "playlistV2": {
                  "name": "{{playlistName}}",
                  "content": { "items": [] }
                }
              }
            }
            """;

        var clientTokenAttempts = 0;

        using var http = new HttpClient(
            new FakeHttpMessageHandler(request =>
            {
                static HttpResponseMessage Ok(string body) =>
                    new(HttpStatusCode.OK) { Content = new StringContent(body) };

                var uri = request.RequestUri!;
                if (uri.Host == SpotifyHost && uri.AbsolutePath == "/")
                    return Ok(SessionHtml);
                if (
                    uri.Host == SpotifyHost
                    && uri.AbsolutePath.StartsWith("/api/token", StringComparison.Ordinal)
                )
                {
                    return Ok(AccessTokenJson);
                }
                if (uri.Host == ClientTokenHost)
                {
                    clientTokenAttempts++;
                    if (clientTokenAttempts == 1)
                        return new HttpResponseMessage(HttpStatusCode.InternalServerError);

                    return Ok(ClientTokenJson);
                }

                return Ok(playlistJson);
            })
        );

        var client = Client(http);

        await Assert.ThrowsAsync<HttpRequestException>(() => client.GetPlaylistAsync("pl-1", Ct));

        var recovered = await client.GetPlaylistAsync("pl-2", Ct);

        Assert.Equal(playlistName, recovered.Name);
        Assert.Equal(2, clientTokenAttempts);
    }
}
