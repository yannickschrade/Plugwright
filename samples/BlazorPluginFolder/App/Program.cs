using BlazorPluginFolder.App.Components;
using Plugwright;
using Plugwright.AspNetCore;

// --8<-- [start:host]
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddRazorComponents();

builder.Plugins.AddFromDirectory(Path.Combine(AppContext.BaseDirectory, "plugins"));
builder.PluginHandlers.Add<BlazorPluginHandler>();
builder.ApplyPlugins();

var app = builder.Build();
app.UseAntiforgery();
app.UsePlugins();

app.MapStaticAssets();                 // the application's own assets
app.MapPluginStaticAssets();           // the assets of plugins from directories and packages
app.MapRazorComponents<App>()
   .AddPluginAssemblies(app);

app.Run();
// --8<-- [end:host]
