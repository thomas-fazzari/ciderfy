using Ciderfy.Apple;
using Ciderfy.Tui;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Ciderfy.Tests;

public class DependencyInjectionTests
{
    [Fact]
    public void AddTui_RegistersTuiAppAsTransient()
    {
        var services = new ServiceCollection();
        services.AddSingleton(new TokenCache());
        services.AddTui();

        using var provider = services.BuildServiceProvider(
            new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true }
        );

        var first = provider.GetRequiredService<TuiApp>();
        var second = provider.GetRequiredService<TuiApp>();

        Assert.NotSame(first, second);
    }
}
