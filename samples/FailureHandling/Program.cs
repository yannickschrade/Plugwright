using FailureHandling;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Plugwright;

// --8<-- [start:host]
var builder = Host.CreateApplicationBuilder(args);

// Skip plugins that fail and log an error, instead of stopping the application.
builder.Plugins.Options.FailureMode = PluginFailureMode.SkipAndLog;

builder.Plugins.Add<BrokenPlugin>();
builder.Plugins.Add<WorkingPlugin>();

builder.ApplyPlugins();
var app = builder.Build();
app.UsePlugins();
// --8<-- [end:host]

// --8<-- [start:report]
// The catalog tells the state of every plugin, for example for a status page.
var catalog = app.Services.GetRequiredService<IPluginCatalog>();
foreach (var plugin in catalog.Plugins.Where(plugin => plugin.State != PluginState.Loaded))
{
    Console.WriteLine($"Not loaded: {plugin.Id} ({plugin.Source}): {plugin.State}, {plugin.Reason}");
}
// --8<-- [end:report]

await app.RunAsync();
