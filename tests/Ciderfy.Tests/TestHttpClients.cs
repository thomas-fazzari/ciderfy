using Ciderfy.Apple;
using Ciderfy.Matching;
using Ciderfy.Web;

namespace Ciderfy.Tests;

internal static class TestHttpClients
{
    internal static HttpClient CreateAppleMusicAuthClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        HttpClientFactory.ConfigureAppleMusicAuthClient(client);
        return client;
    }

    internal static HttpClient CreateAppleMusicClient()
    {
        var client = new HttpClient(HttpClientFactory.CreateDecompressionHandler())
        {
            Timeout = TimeSpan.FromSeconds(30),
            BaseAddress = new Uri(new AppleMusicClientOptions().BaseUrl),
        };
        HttpClientFactory.ConfigureAppleMusicClient(client);
        return client;
    }

    internal static HttpClient CreateDeezerClient()
    {
        var client = new HttpClient(HttpClientFactory.CreateDecompressionHandler())
        {
            Timeout = TimeSpan.FromSeconds(15),
            BaseAddress = new Uri(new DeezerClientOptions().BaseUrl),
        };
        HttpClientFactory.ConfigureDeezerClient(client);
        return client;
    }
}
