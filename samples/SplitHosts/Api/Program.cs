using Acme.Orders;
using Plugwright;
using Plugwright.AspNetCore;

// --8<-- [start:host]
var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();                     // Aspire: telemetry, health checks, service discovery

builder.Plugins.Add<OrdersPlugin>();              // backend part
builder.PluginHandlers.Add<WebPluginHandler>();
builder.ApplyPlugins();                           // after AddServiceDefaults()

var app = builder.Build();
app.UsePlugins();
app.MapDefaultEndpoints();                        // /health includes the plugins' health checks

// Tells the web host which backend plugins run here. Protect it, for example with RequireAuthorization(),
// when the API is reachable from outside.
app.MapPluginCatalog();

app.Run();
// --8<-- [end:host]
