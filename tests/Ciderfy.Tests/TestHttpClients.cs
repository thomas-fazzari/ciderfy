using Ciderfy.Apple;
using Ciderfy.Matching;
using Ciderfy.Web;

namespace Ciderfy.Tests;

internal static class TestHttpClients
{
    internal static HttpClient CreateAppleMusicAuthClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        HttpClientDefaults.ConfigureAppleMusicAuthClient(client);
        return client;
    }

    internal static HttpClient CreateAppleMusicClient()
    {
        var client = new HttpClient(HttpClientDefaults.CreateDecompressionHandler())
        {
            Timeout = TimeSpan.FromSeconds(30),
            BaseAddress = new Uri(AppleMusicClient.BaseUrl),
        };
        HttpClientDefaults.ConfigureAppleMusicClient(client, AppleMusicAuth.BaseUrl);
        return client;
    }

    internal static HttpClient CreateDeezerClient()
    {
        var client = new HttpClient(HttpClientDefaults.CreateDecompressionHandler())
        {
            Timeout = TimeSpan.FromSeconds(15),
            BaseAddress = new Uri(DeezerIsrcResolver.BaseUrl),
        };
        HttpClientDefaults.ConfigureDeezerClient(client);
        return client;
    }
}
