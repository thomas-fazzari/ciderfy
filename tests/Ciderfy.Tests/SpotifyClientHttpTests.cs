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

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static SpotifyClient Client(HttpClient http) => new(http, new CookieContainer());

    [Fact]
    public async Task GetPlaylistAsync_throws_when_network_fails()
    {
        using var http = FakeHttpMessageHandler.ThrowingHttpRequestException();

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            Client(http).GetPlaylistAsync("anyid", Ct)
        );
    }

    [Fact]
    public async Task GetPlaylistAsync_throws_when_request_times_out()
    {
        using var http = FakeHttpMessageHandler.ThrowingTimeoutCanceledException();

        await Assert.ThrowsAsync<TaskCanceledException>(() =>
            Client(http).GetPlaylistAsync("anyid", Ct)
        );
    }

    [Fact]
    public async Task GetPlaylistAsync_returns_playlist_with_tracks()
    {
        var configBase64 = Convert.ToBase64String(
            Encoding.UTF8.GetBytes("""{"clientVersion":"1.2.3"}""")
        );
        var sessionHtml =
            $"""<script id="appServerConfig" type="text/plain">{configBase64}</script>""";

        var playlistJson = """
            {
              "data": {
                "playlistV2": {
                  "name": "My Playlist",
                  "content": {
                    "items": [
                      {
                        "itemV2": {
                          "data": {
                            "name": "Track One",
                            "uri": "spotify:track:abc123",
                            "artists": { "items": [{ "profile": { "name": "Artist A" } }] },
                            "trackDuration": { "totalMilliseconds": 210000 }
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
                    return Ok(sessionHtml);
                if (
                    uri.Host == SpotifyHost
                    && uri.AbsolutePath.StartsWith("/api/token", StringComparison.Ordinal)
                )
                {
                    return Ok("""{"accessToken":"tok","clientId":"cid"}""");
                }
                if (uri.Host == ClientTokenHost)
                    return Ok("""{"granted_token":{"token":"ctok"}}""");
                return Ok(playlistJson);
            })
        );

        var playlist = await Client(http).GetPlaylistAsync("playlist123", Ct);

        Assert.Equal("My Playlist", playlist.Name);
        Assert.Single(playlist.Tracks);
        Assert.Equal("Track One", playlist.Tracks[0].Title);
        Assert.Equal("Artist A", playlist.Tracks[0].Artist);
        Assert.Equal("abc123", playlist.Tracks[0].SpotifyId);
        Assert.Equal(210000, playlist.Tracks[0].DurationMs);
    }
}
