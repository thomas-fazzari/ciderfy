using System.Net;
using Ciderfy.Apple;
using Ciderfy.Configuration.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Ciderfy.DependencyInjection;

internal static class AppleServiceCollectionExtensions
{
    internal static IServiceCollection AddApple(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        services.AddSingleton(TokenCache.Load());

        services
            .AddOptions<AppleMusicClientOptions>()
            .Bind(configuration.GetSection(AppleMusicClientOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services
            .AddOptions<AppleMusicAuthOptions>()
            .Bind(configuration.GetSection(AppleMusicAuthOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

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

        return services;
    }
}
