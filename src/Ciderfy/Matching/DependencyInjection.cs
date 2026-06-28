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
            .Validate(
                o => o is { TimeoutSeconds: > 0, RateLimitDelayMs: >= 0 },
                "Deezer timing options must be positive."
            )
            .ValidateOnStart();

        services
            .AddHttpClient<DeezerIsrcResolver>(
                (sp, client) =>
                {
                    var options = sp.GetRequiredService<IOptions<DeezerClientOptions>>().Value;
                    client.BaseAddress = new Uri(DeezerIsrcResolver.BaseUrl);
                    client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
                    HttpClientDefaults.ConfigureDeezerClient(client);
                }
            )
            .ConfigurePrimaryHttpMessageHandler(HttpClientDefaults.CreateDecompressionHandler)
            .AddStandardResilienceHandler(o =>
            {
                // 429 handled by internal SlidingWindowRateLimiter
                o.Retry.ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                    .Handle<HttpRequestException>()
                    .HandleResult(r =>
                        r.StatusCode
                            is >= HttpStatusCode.InternalServerError
                                or HttpStatusCode.RequestTimeout
                    );
            });

        return services;
    }
}
