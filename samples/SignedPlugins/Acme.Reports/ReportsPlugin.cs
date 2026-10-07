using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Plugwright;

namespace Acme.Reports;

public sealed class ReportsPlugin : IPlugin
{
    public void Configure(PluginContext context)
    {
        context.Builder.Services.AddHostedService<ReportsService>();
    }
}

public sealed class ReportsService(ILogger<ReportsService> logger) : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Reports plugin signed by Acme is running.");
        return Task.CompletedTask;
    }
}
