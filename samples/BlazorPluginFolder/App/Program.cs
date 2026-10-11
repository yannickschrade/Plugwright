using BlazorPluginFolder.App.Components;
using Plugwright;
using Plugwright.AspNetCore;

// --8<-- [start:host]
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddRazorComponents();

// --8<-- [start:sources]
#if PLUGINS_AS_PROJECTS
builder.Plugins.AddFromReferencedAssemblies("Acme");                     // referenced plugin projects
#else
builder.Plugins.AddFromDirectory(Path.Combine(AppContext.BaseDirectory, "plugins"));   // published plugins
#endif
// --8<-- [end:sources]
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
