using Ciderfy.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Ciderfy.Tui;

internal static class TuiExtensions
{
    public static IServiceCollection AddTui(this IServiceCollection services)
    {
        services.AddSingleton<IConfigurationFolderOpener, ConfigurationFolderOpener>();
        services.AddTransient<TuiApp>();
        return services;
    }
}
