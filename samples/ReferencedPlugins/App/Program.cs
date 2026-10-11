using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Plugwright;

// --8<-- [start:host]
var builder = Host.CreateApplicationBuilder(args);

// Every referenced assembly named Acme.Plugins or Acme.Plugins.* is searched for plugins.
builder.Plugins.AddFromReferencedAssemblies("Acme.Plugins");

builder.ApplyPlugins();
var app = builder.Build();
app.UsePlugins();
// --8<-- [end:host]

foreach (var plugin in app.Services.GetRequiredService<IPluginCatalog>().Plugins)
{
    Console.WriteLine($"{plugin.Id}: {plugin.State}");
}
