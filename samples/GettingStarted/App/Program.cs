using Microsoft.Extensions.Hosting;
using Plugwright;

#region host
var builder = Host.CreateApplicationBuilder(args);

// Load every plugin from its own folder: plugins/Acme.Hello/Acme.Hello.dll
builder.Plugins.AddFromDirectory(Path.Combine(AppContext.BaseDirectory, "plugins"));

// Only load plugins whose files match the lock file. Without a verifier, plugins are not verified.
builder.PluginVerifiers.AddHashVerifier(options =>
    options.LockFile = Path.Combine(AppContext.BaseDirectory, "plugins.lock.json"));

builder.ApplyPlugins();   // phase 1: verify, load and configure plugins
var app = builder.Build();
app.UsePlugins();         // phase 2: plugins work with the built host

await app.RunAsync();
#endregion
