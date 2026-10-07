using Microsoft.Extensions.Logging;
using Plugwright.TestPlugins.TextLib;

namespace Plugwright.TestPlugins.Greeter;

/// <summary>
/// Reports through configuration, because the tests share no types with the plugin except the host's.
/// </summary>
public sealed class GreeterPlugin : IPlugin
{
    public void Configure(PluginContext context)
    {
        context.Builder.Configuration["TestPlugins:Greeter:Message"] = context.Configuration["Message"] ?? "Hello";
        context.Builder.Configuration["TestPlugins:Greeter:TextLibVersion"] = TextInfo.Version;
        context.Builder.Configuration["TestPlugins:Greeter:Id"] = context.Metadata.Id;
        context.Logger.LogInformation("Greeter configured");
    }
}

/// <summary>
/// A second plugin class in the same assembly. It shares the plugin ID and the load context.
/// </summary>
public sealed class FarewellPlugin : IPlugin
{
    public void Configure(PluginContext context)
    {
        context.Builder.Configuration["TestPlugins:Farewell:Id"] = context.Metadata.Id;
        context.Builder.Configuration["TestPlugins:Farewell:TextLibVersion"] = TextInfo.Version;
    }
}

/// <summary>
/// Not a plugin: abstract types are ignored.
/// </summary>
public abstract class PluginBase : IPlugin
{
    public abstract void Configure(PluginContext context);
}
