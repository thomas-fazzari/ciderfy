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

    internal static void ConfigureAppleMusicAuthClient(HttpClient client)
    {
        client.DefaultRequestHeaders.Add("User-Agent", AppleMusicAuthUserAgent);
    }

    internal static void ConfigureAppleMusicClient(HttpClient client)
    {
        client.DefaultRequestHeaders.Add("User-Agent", AppleMusicClientUserAgent);
        client.DefaultRequestHeaders.Add("Origin", "https://music.apple.com");
    }

    internal static void ConfigureDeezerClient(HttpClient client)
    {
        client.DefaultRequestHeaders.Add("User-Agent", DeezerUserAgent);
    }

    internal static HttpClientHandler CreateDecompressionHandler() =>
        new() { AutomaticDecompression = DecompressionMethods.All };
}
