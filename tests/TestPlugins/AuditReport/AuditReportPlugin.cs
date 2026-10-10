using Microsoft.Extensions.Configuration;
using Plugwright;

[assembly: PluginDependency("Plugwright.TestPlugins.Audit")]

namespace Plugwright.TestPlugins.AuditReport;

public sealed class AuditReportPlugin : IPlugin
{
    public void Configure(PluginContext context)
    {
        context.Builder.Configuration["TestPlugins:Order"] += "AuditReport;";
        context.AddStartup<AuditReportStartup>();
    }
}

public sealed class AuditReportStartup(IConfiguration configuration) : IPluginStartup
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        configuration["TestPlugins:Startup"] += "AuditReport;";
        return Task.CompletedTask;
    }
}
