using Ciderfy.Tui;
using Microsoft.Extensions.DependencyInjection;

namespace Ciderfy.DependencyInjection;

internal static class TuiServiceCollectionExtensions
{
    internal static IServiceCollection AddTui(this IServiceCollection services)
    {
        services.AddTransient<TuiApp>();
        return services;
    }
}
