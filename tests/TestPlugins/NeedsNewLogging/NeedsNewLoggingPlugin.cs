using Plugwright;

[assembly: PluginDependency("Plugwright.TestPlugins.Logging", MinVersion = "9.0.0")]

namespace Plugwright.TestPlugins.NeedsNewLogging;

public sealed class NeedsNewLoggingPlugin : IPlugin
{
    public void Configure(PluginContext context)
    {
        context.Builder.Configuration["TestPlugins:Order"] += "NeedsNewLogging;";
    }
}
