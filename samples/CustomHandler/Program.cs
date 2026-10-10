using CustomHandler;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Plugwright;

// --8<-- [start:host]
var builder = Host.CreateApplicationBuilder(args);

builder.PluginHandlers.Add<GreetingPluginHandler>();
builder.Plugins.Add<EnglishGreetings>();
builder.Plugins.Add<GermanGreetings>();

builder.ApplyPlugins();
var app = builder.Build();
app.UsePlugins();

foreach (var greeting in app.Services.GetServices<Greeting>())
{
    Console.WriteLine($"{greeting.Text} (from {greeting.PluginId})");
}
// --8<-- [end:host]
