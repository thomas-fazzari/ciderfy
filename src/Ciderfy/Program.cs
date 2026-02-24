using Ciderfy.DependencyInjection;
using Ciderfy.Tui;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = Host.CreateApplicationBuilder(args);
builder.Logging.ClearProviders();
builder.Services.AddCiderfy(builder.Configuration);

using var host = builder.Build();
await host.StartAsync();

var app = host.Services.GetRequiredService<TuiApp>();
var exitCode = await app.RunAsync();

await host.StopAsync();
return exitCode;
