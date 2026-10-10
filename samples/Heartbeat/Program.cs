using Heartbeat;
using Microsoft.Extensions.Hosting;
using Plugwright;

// --8<-- [start:host]
var builder = Host.CreateApplicationBuilder(args);

builder.Plugins.Add<HeartbeatPlugin>();

builder.ApplyPlugins();   // phase 1: plugins register their services
var app = builder.Build();
app.UsePlugins();         // phase 2: plugins work with the built host

await app.RunAsync();
// --8<-- [end:host]
