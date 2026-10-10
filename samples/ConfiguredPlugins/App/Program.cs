using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Plugwright;

// --8<-- [start:host]
// The content root is the application's folder, so that relative paths in appsettings.json point there.
var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
{
    Args = args,
    ContentRootPath = AppContext.BaseDirectory,
});

// Package folders and the package signature verifier come from Plugwright.NuGet.
builder.Plugins.AddNuGetSupport();

// From here on, the configuration decides which plugins are loaded and how they are verified.
var section = builder.Configuration.GetSection("Plugwright");
builder.Plugins.AddFromConfiguration(section);
builder.PluginVerifiers.AddFromConfiguration(section.GetSection("Verification"));

builder.ApplyPlugins();
var app = builder.Build();
app.UsePlugins();

await app.RunAsync();
// --8<-- [end:host]
