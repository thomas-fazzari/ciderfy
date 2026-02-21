using Ciderfy;
using Ciderfy.Tui;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();
services.AddCiderfy();

await using var provider = services.BuildServiceProvider();
var app = provider.GetRequiredService<TuiApp>();

return await app.RunAsync();
