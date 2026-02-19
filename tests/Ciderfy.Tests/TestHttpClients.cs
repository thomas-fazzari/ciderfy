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
        };
        HttpClientFactory.ConfigureAppleMusicClient(client);
        return client;
    }

    internal static HttpClient CreateDeezerClient()
    {
        var client = new HttpClient(HttpClientFactory.CreateDecompressionHandler())
        {
            Timeout = TimeSpan.FromSeconds(15),
        };
        HttpClientFactory.ConfigureDeezerClient(client);
        return client;
    }
}
