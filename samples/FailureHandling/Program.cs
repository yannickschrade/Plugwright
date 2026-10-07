using FailureHandling;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Plugwright;

#region host
var builder = Host.CreateApplicationBuilder(args);

// Skip plugins that fail and log an error, instead of stopping the application.
builder.Plugins.Options.FailureMode = PluginFailureMode.SkipAndLog;

builder.Plugins.Add<BrokenPlugin>();
builder.Plugins.Add<WorkingPlugin>();

builder.ApplyPlugins();
var app = builder.Build();
app.UsePlugins();
#endregion

#region report
// The report tells which plugins run and which were skipped, for example for a status page.
var report = app.Services.GetRequiredService<PluginReport>();
foreach (var skipped in report.SkippedPlugins)
{
    Console.WriteLine($"Skipped: {skipped.PluginId} ({skipped.Source}): {skipped.Reason}");
}
#endregion

await app.RunAsync();
