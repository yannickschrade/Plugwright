using Microsoft.Extensions.DependencyInjection;
using Plugwright.TestPackages.Library;

namespace Plugwright.TestPackages.Greeter;

/// <summary>
/// A type that the Reports plugin package uses. It must be the same type for both.
/// </summary>
public sealed class Greeting
{
    public Greeting()
    {
        Text = $"Hello from library version {LibraryInfo.Version}";
    }

    public string Text { get; }
}

public sealed class GreeterPlugin : IPlugin
{
    public void Configure(PluginContext context)
    {
        context.Builder.Configuration["TestPackages:Order"] += "Greeter;";
        context.Builder.Configuration["TestPackages:Greeter:LibraryVersion"] = LibraryInfo.Version;
        context.Builder.Configuration["TestPackages:Greeter:Description"] = context.Metadata.Description;
        context.Builder.Configuration["TestPackages:Greeter:Message"] = context.Configuration["Message"];
        context.Builder.Services.AddSingleton<Greeting>();
    }
}
