using Ciderfy.Apple;
using Ciderfy.Matching;
using Ciderfy.Spotify;
using Ciderfy.Tui;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Ciderfy;

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
