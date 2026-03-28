using System.Net;

namespace Ciderfy.Web;

/// <summary>
/// Shared HTTP client configuration used by both the DI extensions and integration test helpers
/// </summary>
internal static class HttpClientFactory
{
    internal const string AppleMusicAuthUserAgent =
        "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 "
        + "(KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";

    internal const string AppleMusicClientUserAgent =
        "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7)";

    internal const string DeezerUserAgent = "Ciderfy/1.0 (playlist transfer tool)";

    internal const string SpotifyUserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 "
        + "(KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";

    internal const string AppleMusicUrl = "https://music.apple.com";

    internal static void ConfigureAppleMusicAuthClient(HttpClient client)
    {
        client.DefaultRequestHeaders.Add(HttpHeaderNames.UserAgent, AppleMusicAuthUserAgent);
    }

    internal static void ConfigureAppleMusicClient(HttpClient client)
    {
        client.DefaultRequestHeaders.Add(HttpHeaderNames.UserAgent, AppleMusicClientUserAgent);
        client.DefaultRequestHeaders.Add(HttpHeaderNames.Origin, AppleMusicUrl);
    }

    internal static void ConfigureDeezerClient(HttpClient client)
    {
        client.DefaultRequestHeaders.Add(HttpHeaderNames.UserAgent, DeezerUserAgent);
    }

    internal static void ConfigureSpotifyClient(HttpClient client)
    {
        client.DefaultRequestHeaders.Add(HttpHeaderNames.UserAgent, SpotifyUserAgent);
    }

    internal static HttpClientHandler CreateDecompressionHandler() =>
        new() { AutomaticDecompression = DecompressionMethods.All };
}
