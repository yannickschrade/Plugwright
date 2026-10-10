using Acme.Formatting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Plugwright;

// --8<-- [start:dependency]
// The reference to Acme.Formatting is already a dependency. The attribute adds a minimum version.
[assembly: PluginDependency("Acme.Formatting", MinVersion = "1.2.0")]
// --8<-- [end:dependency]

namespace Acme.Welcome;

// --8<-- [start:plugin]
public sealed class WelcomePlugin : IPlugin
{
    public void Configure(PluginContext context)
    {
        context.Builder.Services.AddHostedService<WelcomeService>();
    }
}

// Receives the formatter that the Acme.Formatting plugin registered.
public sealed class WelcomeService(IGreetingFormatter formatter, ILogger<WelcomeService> logger) : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("{Greeting}", formatter.Format("World"));
        return Task.CompletedTask;
    }
}
// --8<-- [end:plugin]
