using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Plugwright;
using SharedAssemblies.Contracts;

// --8<-- [start:host]
var builder = Host.CreateApplicationBuilder(args);

builder.Plugins.AddFromDirectory(Path.Combine(AppContext.BaseDirectory, "plugins"));

// Plugins use the host's copy of the contract, even if they ship their own.
builder.Plugins.ShareAssembly(typeof(IStatusSource).Assembly);

builder.ApplyPlugins();
var app = builder.Build();
app.UsePlugins();

// Without ShareAssembly, the plugin's IStatusSource would be a different type and this list would be empty.
foreach (var source in app.Services.GetServices<IStatusSource>())
{
    Console.WriteLine($"{source.Name}: {source.GetStatus()}");
}
// --8<-- [end:host]
