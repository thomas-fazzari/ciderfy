using Ciderfy.Tui;
using Microsoft.Extensions.DependencyInjection;

namespace Ciderfy.DependencyInjection;

internal static class TuiExtensions
{
    public static IServiceCollection AddTui(this IServiceCollection services)
    {
        services.AddSingleton<TuiApp>();
        return services;
    }
}
