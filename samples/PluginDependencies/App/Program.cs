using Microsoft.Extensions.Hosting;
using Plugwright;

// --8<-- [start:host]
var builder = Host.CreateApplicationBuilder(args);

// Acme.Welcome depends on Acme.Formatting. Plugwright checks the dependency before loading any plugin
// and applies Acme.Formatting first.
builder.Plugins.AddFromDirectory(Path.Combine(AppContext.BaseDirectory, "plugins"));

builder.ApplyPlugins();
var app = builder.Build();
app.UsePlugins();

await app.RunAsync();
// --8<-- [end:host]
