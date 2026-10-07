using Plugwright;

[assembly: PluginDependency("Plugwright.TestPlugins.CycleA")]

namespace Plugwright.TestPlugins.CycleB;

public sealed class CycleBPlugin : IPlugin
{
    public void Configure(PluginContext context)
    {
        context.Builder.Configuration["TestPlugins:Order"] += "CycleB;";
    }
}
