using System.Net;
using Ciderfy.Apple;
using Ciderfy.Configuration;
using Ciderfy.Matching;
using Ciderfy.Spotify;
using Ciderfy.Tui;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Ciderfy;

internal static class DependencyInjection
{
    internal static IServiceCollection AddCiderfy(this IServiceCollection services)
    {
        services.AddSingleton(TokenCache.Load());
        services.AddTransient<TuiApp>();
        services.AddTransient<TrackMatcher>();
        services.AddTransient<PlaylistTransferService>();
        services.AddTransient<CookieContainer>();

        services
            .AddOptions<AppleMusicClientOptions>()
            .Validate(options => options.TimeoutSeconds > 0)
            .Validate(options => options.MinDelayBetweenCallsMs >= 0);

        services
            .AddOptions<AppleMusicAuthOptions>()
            .Validate(options => options.TimeoutSeconds > 0);

        services
            .AddOptions<DeezerClientOptions>()
            .Validate(options => options.TimeoutSeconds > 0)
            .Validate(options => options.RateLimitDelayMs >= 0);

        services.AddOptions<SpotifyClientOptions>().Validate(options => options.TimeoutSeconds > 0);

        services
            .AddHttpClient<AppleMusicClient>(
                (sp, client) =>
                {
                    var options = sp.GetRequiredService<IOptions<AppleMusicClientOptions>>().Value;
                    client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
                    client.DefaultRequestHeaders.Add(
                        "User-Agent",
                        "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7)"
                    );
                    client.DefaultRequestHeaders.Add("Origin", "https://music.apple.com");
                }
            )
            .ConfigurePrimaryHttpMessageHandler(() =>
                new HttpClientHandler { AutomaticDecompression = DecompressionMethods.All }
            );

        services.AddHttpClient<AppleMusicAuth>(
            (sp, client) =>
            {
                var options = sp.GetRequiredService<IOptions<AppleMusicAuthOptions>>().Value;
                client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
                client.DefaultRequestHeaders.Add(
                    "User-Agent",
                    "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 "
                        + "(KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36"
                );
            }
        );

        services
            .AddHttpClient<DeezerIsrcResolver>(
                (sp, client) =>
                {
                    var options = sp.GetRequiredService<IOptions<DeezerClientOptions>>().Value;
                    client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
                    client.DefaultRequestHeaders.Add(
                        "User-Agent",
                        "Ciderfy/1.0 (playlist transfer tool)"
                    );
                }
            )
            .ConfigurePrimaryHttpMessageHandler(() =>
                new HttpClientHandler { AutomaticDecompression = DecompressionMethods.All }
            );

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
