using Acme.Orders.Ui;
using Plugwright.AspNetCore;
using Plugwright;
using SplitHosts.Web.Components;
using SplitHosts.Web;

// --8<-- [start:host]
var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
builder.Services.AddRazorComponents().AddInteractiveServerComponents();

// Turn off UI plugins whose backend part is not loaded in the API host. App code, not part of Plugwright.
await BackendPlugins.TurnOffUiPluginsWithoutBackendAsync(builder, new() { ["Acme.Orders.Ui"] = "Acme.Orders" });

builder.Plugins.Add<OrdersUiPlugin>();            // UI part
builder.PluginHandlers.Add<BlazorPluginHandler>();
builder.ApplyPlugins();

var app = builder.Build();
app.UseAntiforgery();
app.UsePlugins();
app.MapDefaultEndpoints();
app.MapStaticAssets();
app.MapRazorComponents<App>()
   .AddInteractiveServerRenderMode()
   .AddPluginAssemblies(app);

app.Run();
// --8<-- [end:host]
