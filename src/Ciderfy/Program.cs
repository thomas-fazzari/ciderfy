using Ciderfy;
using Ciderfy.Tui;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var ct = CancellationToken.None;
var builder = Host.CreateApplicationBuilder(args);
builder.Logging.ClearProviders();
builder.Services.AddCiderfy(builder.Configuration);

using var host = builder.Build();
await host.StartAsync(ct).ConfigureAwait(false);

var app = host.Services.GetRequiredService<TuiApp>();
var exitCode = await app.RunAsync().ConfigureAwait(false);

await host.StopAsync(ct).ConfigureAwait(false);
return exitCode;
