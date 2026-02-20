using Ciderfy;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();
services.AddCiderfy();

await using var provider = services.BuildServiceProvider();
var app = provider.GetRequiredService<App>();

return await app.RunAsync();
