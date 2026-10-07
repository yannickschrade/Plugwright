using Plugwright;

[assembly: PluginDependency("Plugwright.TestPlugins.CycleB")]

namespace Plugwright.TestPlugins.CycleA;

public sealed class CycleAPlugin : IPlugin
{
    public void Configure(PluginContext context)
    {
        context.Builder.Configuration["TestPlugins:Order"] += "CycleA;";
    }
}
