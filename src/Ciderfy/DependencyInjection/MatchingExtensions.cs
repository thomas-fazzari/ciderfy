using Ciderfy.Configuration.Options;
using Ciderfy.Matching;
using Ciderfy.Web;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Ciderfy.DependencyInjection;

internal static class MatchingExtensions
{
    extension<T>(T services)
        where T : IServiceCollection
    {
        public IServiceCollection AddMatching(IConfiguration configuration)
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
                        HttpClientFactory.ConfigureDeezerClient(client);
                    }
                )
                .ConfigurePrimaryHttpMessageHandler(HttpClientFactory.CreateDecompressionHandler);

            return services;
        }
    }
}
