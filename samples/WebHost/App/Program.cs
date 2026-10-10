using Acme.Greetings;
using Plugwright;

#region host
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();

builder.Plugins.Add<GreetingsPlugin>();
builder.PluginHandlers.Add<WebPluginHandler>();   // controllers, endpoints and middleware of web plugins
builder.ApplyPlugins();                           // phase 1

var app = builder.Build();
app.UsePlugins();                                 // phase 2: plugin middleware runs here, plugin endpoints are mapped
app.MapControllers();

// The host uses a service the plugin registered.
app.MapGet("/", (IGreeter greeter) => greeter.Greet("web host"));

app.Run();
#endregion
