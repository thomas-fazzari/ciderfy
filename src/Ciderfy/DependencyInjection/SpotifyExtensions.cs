using System.Net;
using Ciderfy.Configuration.Options;
using Ciderfy.Spotify;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Ciderfy.DependencyInjection;

internal static class SpotifyExtensions
{
    extension<T>(T services)
        where T : IServiceCollection
    {
        public IServiceCollection AddSpotify(IConfiguration configuration)
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
                        client.DefaultRequestHeaders.Add(
                            "User-Agent",
                            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 "
                                + "(KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36"
                        );
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
}
