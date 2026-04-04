using System.Net;
using Ciderfy.Configuration.Options;
using Ciderfy.Spotify;
using Ciderfy.Web;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Ciderfy.DependencyInjection;

internal static class SpotifyExtensions
{
    public static IServiceCollection AddSpotify(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        services
            .AddOptions<SpotifyClientOptions>()
            .Bind(configuration.GetSection(SpotifyClientOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton<CookieContainer>();

        services
            .AddHttpClient<SpotifyClient>(
                (sp, client) =>
                {
                    var options = sp.GetRequiredService<IOptions<SpotifyClientOptions>>().Value;
                    client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
                    HttpClientFactory.ConfigureSpotifyClient(client);
                }
            )
            .ConfigurePrimaryHttpMessageHandler(sp => new HttpClientHandler
            {
                CookieContainer = sp.GetRequiredService<CookieContainer>(),
                UseCookies = true,
            });

        return services;
    }
}
