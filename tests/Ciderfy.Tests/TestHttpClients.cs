using System.Net;

namespace Ciderfy.Tests;

internal static class TestHttpClients
{
    internal static HttpClient CreateAppleMusicAuthClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        client.DefaultRequestHeaders.Add(
            "User-Agent",
            "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 "
                + "(KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36"
        );

        return client;
    }

    internal static HttpClient CreateAppleMusicClient()
    {
        var handler = new HttpClientHandler { AutomaticDecompression = DecompressionMethods.All };
        var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(30) };
        client.DefaultRequestHeaders.Add(
            "User-Agent",
            "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7)"
        );
        client.DefaultRequestHeaders.Add("Origin", "https://music.apple.com");

        return client;
    }

    internal static HttpClient CreateDeezerClient()
    {
        var handler = new HttpClientHandler { AutomaticDecompression = DecompressionMethods.All };
        var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(15) };
        client.DefaultRequestHeaders.Add("User-Agent", "Ciderfy/1.0 (playlist transfer tool)");

        return client;
    }
}
