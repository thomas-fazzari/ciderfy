using System.Net;
using Ciderfy.Web;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Polly;

namespace Ciderfy.Apple;

internal static class AppleExtensions
{
    public static IServiceCollection AddApple(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        services.AddSingleton(TokenCache.Load());

        services
            .AddOptions<AppleMusicClientOptions>()
            .Bind(configuration.GetSection(AppleMusicClientOptions.SectionName))
            .Validate(
                o => o is { TimeoutSeconds: > 0, MinDelayBetweenCallsMs: >= 0 },
                "Apple Music client timing options must be positive."
            )
            .ValidateOnStart();

        services
            .AddOptions<AppleMusicAuthOptions>()
            .Bind(configuration.GetSection(AppleMusicAuthOptions.SectionName))
            .Validate(o => o.TimeoutSeconds > 0, "Apple Music auth timeout must be positive.")
            .ValidateOnStart();

        services
            .AddHttpClient<AppleMusicClient>(
                (sp, client) =>
                {
                    var options = sp.GetRequiredService<IOptions<AppleMusicClientOptions>>().Value;
                    client.BaseAddress = new Uri(AppleMusicClient.BaseUrl);
                    client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
                    HttpClientDefaults.ConfigureAppleMusicClient(
                        client,
                        new Uri(AppleMusicAuth.BaseUrl).GetLeftPart(UriPartial.Authority)
                    );
                }
            )
            .ConfigurePrimaryHttpMessageHandler(HttpClientDefaults.CreateDecompressionHandler)
            .AddStandardResilienceHandler(o =>
            {
                // 429 handled manually via AppleMusicRateLimitException
                o.Retry.ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                    .Handle<HttpRequestException>()
                    .HandleResult(r =>
                        r.StatusCode >= HttpStatusCode.InternalServerError
                        || r.StatusCode is HttpStatusCode.RequestTimeout
                    );
            });

        services
            .AddHttpClient<AppleMusicAuth>(
                (sp, client) =>
                {
                    var options = sp.GetRequiredService<IOptions<AppleMusicAuthOptions>>().Value;
                    client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
                    HttpClientDefaults.ConfigureAppleMusicAuthClient(client);
                }
            )
            .AddStandardResilienceHandler();

        return services;
    }
}
