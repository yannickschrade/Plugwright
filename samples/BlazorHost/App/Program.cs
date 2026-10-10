using Acme.Orders.Ui;
using BlazorHost.App.Components;
using Plugwright;

// --8<-- [start:host]
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddRazorComponents().AddInteractiveServerComponents();

builder.Plugins.Add<OrdersUiPlugin>();
builder.PluginHandlers.Add<BlazorPluginHandler>();   // Razor components of Blazor plugins
builder.ApplyPlugins();                              // phase 1

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/error", createScopeForErrors: true);
}

app.UseAntiforgery();
app.UsePlugins();                                    // phase 2

app.MapStaticAssets();
app.MapRazorComponents<App>()
   .AddInteractiveServerRenderMode()
   .AddPluginAssemblies(app);                        // endpoints for the plugins' pages

app.Run();
// --8<-- [end:host]
