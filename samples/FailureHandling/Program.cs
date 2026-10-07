using FailureHandling;
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

await app.RunAsync();
#endregion
