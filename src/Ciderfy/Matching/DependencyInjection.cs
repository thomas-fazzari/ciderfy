using System.Net;
using Ciderfy.Web;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Polly;

namespace Ciderfy.Matching;

internal static class MatchingExtensions
{
    public static IServiceCollection AddMatching(
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
                    client.BaseAddress = new Uri(options.BaseUrl);
                    client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
                    HttpClientFactory.ConfigureDeezerClient(client);
                }
            )
            .ConfigurePrimaryHttpMessageHandler(HttpClientFactory.CreateDecompressionHandler)
            .AddStandardResilienceHandler(o =>
            {
                // 429 handled by internal SlidingWindowRateLimiter
                o.Retry.ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                    .Handle<HttpRequestException>()
                    .HandleResult(r =>
                        r.StatusCode >= HttpStatusCode.InternalServerError
                        || r.StatusCode is HttpStatusCode.RequestTimeout
                    );
            });

        return services;
    }
}
