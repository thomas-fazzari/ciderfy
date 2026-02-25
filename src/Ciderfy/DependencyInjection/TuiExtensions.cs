using Ciderfy.Tui;
using Microsoft.Extensions.DependencyInjection;

namespace Ciderfy.DependencyInjection;

internal static class TuiExtensions
{
    extension<T>(T services)
        where T : IServiceCollection
    {
        public IServiceCollection AddTui()
        {
            services.AddTransient<TuiApp>();
            return services;
        }
    }
}
