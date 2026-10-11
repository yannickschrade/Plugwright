using CustomActivation.App;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Plugwright;

// --8<-- [start:host]
var builder = Host.CreateApplicationBuilder(args);

// The plugin states come from the database. Add the source before ApplyPlugins(), which reads them.
var connectionString = builder.Configuration.GetConnectionString("Plugins") ?? "Data Source=plugins.db";
builder.Configuration.Sources.Add(new PluginStateConfigurationSource(connectionString));

// The database decides alone: no pluginsettings.json, which would win over it.
builder.Plugins.Options.SettingsFile = null;

builder.Plugins.AddFromReferencedAssemblies("Acme.Plugins");
builder.ApplyPlugins();
var app = builder.Build();
app.UsePlugins();
// --8<-- [end:host]

foreach (var plugin in app.Services.GetRequiredService<IPluginCatalog>().Plugins)
{
    Console.WriteLine($"{plugin.Id}: {plugin.State}");
}
