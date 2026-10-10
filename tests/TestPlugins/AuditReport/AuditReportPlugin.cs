using Plugwright;

[assembly: PluginDependency("Plugwright.TestPlugins.Audit")]

namespace Plugwright.TestPlugins.AuditReport;

public sealed class AuditReportPlugin : IPlugin
{
    public void Configure(PluginContext context) => context.Builder.Configuration["TestPlugins:Order"] += "AuditReport;";
}
