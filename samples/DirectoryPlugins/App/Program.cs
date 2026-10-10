using Microsoft.Extensions.Hosting;
using Plugwright;

// --8<-- [start:host]
var builder = Host.CreateApplicationBuilder(args);

// Every subfolder is a plugin: plugins/Acme.Greeter/Acme.Greeter.dll
builder.Plugins.AddFromDirectory(Path.Combine(AppContext.BaseDirectory, "plugins"));

builder.ApplyPlugins();
var app = builder.Build();
app.UsePlugins();

await app.RunAsync();
// --8<-- [end:host]
