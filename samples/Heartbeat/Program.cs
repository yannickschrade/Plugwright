using Heartbeat;
using Microsoft.Extensions.Hosting;
using Plugwright;

#region host
var builder = Host.CreateApplicationBuilder(args);

builder.Plugins.Add<HeartbeatPlugin>();

builder.ApplyPlugins();   // phase 1: plugins register their services
var app = builder.Build();
app.UsePlugins();         // phase 2: plugins work with the built host

await app.RunAsync();
#endregion
