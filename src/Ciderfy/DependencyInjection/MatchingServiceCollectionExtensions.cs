using System.Net;
using Ciderfy.Configuration.Options;
using Ciderfy.Matching;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Ciderfy.DependencyInjection;

internal static class MatchingServiceCollectionExtensions
{
    internal static IServiceCollection AddMatching(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        services.AddTransient<TrackMatcher>();
        services.AddTransient<PlaylistTransferService>();

        services
            .AddOptions<DeezerClientOptions>()
            .Bind(configuration.GetSection(DeezerClientOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

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

        return services;
    }
}
