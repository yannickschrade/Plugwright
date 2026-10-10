using Plugwright;
using Plugwright.AspNetCore;
using Plugwright.TestWebHost.Components;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddRazorComponents();
builder.Services.AddOpenApi();

// "Before", "After" or "None": where the host registers MVC, relative to ApplyPlugins().
var controllers = builder.Configuration["TestWebHost:Controllers"] ?? "After";
if (controllers == "Before")
{
    builder.Services.AddControllers();
}

builder.Plugins.AddFromConfiguration(builder.Configuration.GetSection("Plugwright"));
foreach (var required in builder.Configuration.GetSection("TestWebHost:Require").GetChildren())
{
    builder.Plugins.Require(required.Value!);
}

// --8<-- [start:handlers]
builder.PluginHandlers.Add<WebPluginHandler>().Add<BlazorPluginHandler>();
// --8<-- [end:handlers]
builder.ApplyPlugins();

if (controllers == "After")
{
    builder.Services.AddControllers();
}

var app = builder.Build();

app.Use((context, next) =>
{
    context.Response.Headers.Append("X-Pipeline", "host-before");
    return next(context);
});

app.UsePlugins();

app.Use((context, next) =>
{
    context.Response.Headers.Append("X-Pipeline", "host-after");
    return next(context);
});

app.UseAntiforgery();

if (controllers != "None")
{
    app.MapControllers();
}

app.MapOpenApi();
app.MapPluginCatalog();
app.MapPluginCatalog("/_plugwright/plugins-with-errors", includeErrorMessages: true);
app.MapRazorComponents<App>().AddPluginAssemblies(app);

app.Run();
