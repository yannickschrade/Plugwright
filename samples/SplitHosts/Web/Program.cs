using Acme.Orders.Ui;
using Plugwright;
using SplitHosts.Web.Components;

#region host
var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
builder.Services.AddRazorComponents().AddInteractiveServerComponents();

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
#endregion
