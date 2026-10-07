using Microsoft.Extensions.Hosting;
using Plugwright;

#region host
var builder = Host.CreateApplicationBuilder(args);

// Every package with the package type PlugwrightPlugin is a plugin; the others are library packages.
builder.Plugins.AddFromPackages(Path.Combine(AppContext.BaseDirectory, "packages"));

builder.ApplyPlugins();
var app = builder.Build();
app.UsePlugins();

await app.RunAsync();
#endregion
