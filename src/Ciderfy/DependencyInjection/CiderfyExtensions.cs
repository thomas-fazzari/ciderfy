using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Ciderfy.DependencyInjection;

internal static class CiderfyExtensions
{
    extension<T>(T services)
        where T : IServiceCollection
    {
        public IServiceCollection AddCiderfy(IConfiguration configuration) =>
            services
                .AddApple(configuration)
                .AddSpotify(configuration)
                .AddMatching(configuration)
                .AddTui();
    }
}
