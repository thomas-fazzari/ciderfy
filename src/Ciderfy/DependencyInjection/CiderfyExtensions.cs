using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Ciderfy.DependencyInjection;

internal static class CiderfyExtensions
{
    public static IServiceCollection AddCiderfy(
        this IServiceCollection services,
        IConfiguration configuration
    ) =>
        services
            .AddApple(configuration)
            .AddSpotify(configuration)
            .AddMatching(configuration)
            .AddTui();
}
