using Microsoft.Extensions.Hosting;
using Plugwright;

#region host
var builder = Host.CreateApplicationBuilder(args);

// Every package with the package type PlugwrightPlugin is a plugin; the others are library packages.
builder.Plugins.AddFromPackages(Path.Combine(AppContext.BaseDirectory, "packages"));

// Only load packages, plugins and libraries alike, that match the lock file.
builder.PluginVerifiers.AddHashVerifier(options =>
    options.LockFile = Path.Combine(AppContext.BaseDirectory, "plugins.lock.json"));

builder.ApplyPlugins();
var app = builder.Build();
app.UsePlugins();

await app.RunAsync();
#endregion
