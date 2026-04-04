using Ciderfy.Apple;
using Ciderfy.Configuration.Options;
using Ciderfy.Web;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Ciderfy.DependencyInjection;

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
                    HttpClientFactory.ConfigureAppleMusicClient(client);
                }
            )
            .ConfigurePrimaryHttpMessageHandler(HttpClientFactory.CreateDecompressionHandler);

        services.AddHttpClient<AppleMusicAuth>(
            (sp, client) =>
            {
                var options = sp.GetRequiredService<IOptions<AppleMusicAuthOptions>>().Value;
                client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
                HttpClientFactory.ConfigureAppleMusicAuthClient(client);
            }
        );

        return services;
    }
}
