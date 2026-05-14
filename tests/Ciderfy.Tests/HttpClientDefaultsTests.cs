using System.Net;
using Ciderfy.Web;
using Xunit;

namespace Ciderfy.Tests;

public class HttpClientDefaultsTests
{
    [Fact]
    public void ConfigureAppleMusicAuthClient_AddsUserAgent()
    {
        using var client = new HttpClient();

        HttpClientDefaults.ConfigureAppleMusicAuthClient(client);

        AssertHeader(client, HttpHeaderNames.UserAgent, HttpClientDefaults.AppleMusicAuthUserAgent);
    }

    [Fact]
    public void ConfigureAppleMusicClient_AddsUserAgentAndOrigin()
    {
        using var client = new HttpClient();
        const string origin = "https://music.apple.com";

        HttpClientDefaults.ConfigureAppleMusicClient(client, origin);

        AssertHeader(
            client,
            HttpHeaderNames.UserAgent,
            HttpClientDefaults.AppleMusicClientUserAgent
        );
        AssertHeader(client, HttpHeaderNames.Origin, origin);
    }

    [Fact]
    public void ConfigureDeezerClient_AddsUserAgent()
    {
        using var client = new HttpClient();

        HttpClientDefaults.ConfigureDeezerClient(client);

        AssertHeader(client, HttpHeaderNames.UserAgent, HttpClientDefaults.DeezerUserAgent);
    }

    [Fact]
    public void ConfigureSpotifyClient_AddsUserAgent()
    {
        using var client = new HttpClient();

        HttpClientDefaults.ConfigureSpotifyClient(client);

        AssertHeader(client, HttpHeaderNames.UserAgent, HttpClientDefaults.SpotifyUserAgent);
    }

    [Fact]
    public void CreateDecompressionHandler_EnablesAllDecompressionMethods()
    {
        using var handler = HttpClientDefaults.CreateDecompressionHandler();

        Assert.Equal(DecompressionMethods.All, handler.AutomaticDecompression);
    }

    private static void AssertHeader(HttpClient client, string name, string expected)
    {
        Assert.True(client.DefaultRequestHeaders.TryGetValues(name, out var values));
        Assert.Equal(expected, string.Join(' ', values));
    }
}
