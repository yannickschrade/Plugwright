using Acme.Formatting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Plugwright;

// --8<-- [start:dependency]
// Acme.Farewell runs without Acme.Formatting, and uses it when it is installed.
[assembly: PluginDependency("Acme.Formatting", MinVersion = "1.2.0", IsOptional = true)]
// --8<-- [end:dependency]

namespace Acme.Farewell;

// --8<-- [start:plugin]
public sealed class FarewellPlugin : IPlugin
{
    public void Configure(PluginContext context)
    {
        if (context.IsPluginLoaded("Acme.Formatting"))
        {
            // Only this branch touches types of Acme.Formatting.
            context.Builder.Services.AddHostedService<FormattedFarewellService>();
        }
        else
        {
            context.Builder.Services.AddHostedService<PlainFarewellService>();
        }
    }
}

public sealed class FormattedFarewellService(IGreetingFormatter formatter, ILogger<FarewellPlugin> logger) : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("{Farewell}", formatter.Format("and goodbye"));
        return Task.CompletedTask;
    }
}

public sealed class PlainFarewellService(ILogger<FarewellPlugin> logger) : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Goodbye");
        return Task.CompletedTask;
    }
}
// --8<-- [end:plugin]
