using Acme.Greetings;
using Plugwright;

// --8<-- [start:host]
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();

builder.Plugins.Add<GreetingsPlugin>();
builder.PluginHandlers.Add<WebPluginHandler>();   // controllers, endpoints and middleware of web plugins
builder.ApplyPlugins();                           // phase 1

var app = builder.Build();
app.UsePlugins();                                 // phase 2: plugin middleware runs here, plugin endpoints are mapped
app.MapControllers();

// The host uses a service the plugin registered. The plugin can be turned off, so the service may be missing.
app.MapGet("/", (IServiceProvider services) =>
    services.GetService<IGreeter>()?.Greet("web host") ?? "The greetings plugin is turned off.");

app.Run();
// --8<-- [end:host]
