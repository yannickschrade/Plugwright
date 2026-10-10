using Acme.Orders;
using Plugwright;

#region host
var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();                     // Aspire: telemetry, health checks, service discovery

builder.Plugins.Add<OrdersPlugin>();              // backend part
builder.PluginHandlers.Add<WebPluginHandler>();
builder.ApplyPlugins();                           // after AddServiceDefaults()

var app = builder.Build();
app.UsePlugins();
app.MapDefaultEndpoints();                        // /health includes the plugins' health checks

app.Run();
#endregion
