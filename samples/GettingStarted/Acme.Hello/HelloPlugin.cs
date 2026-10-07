using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Plugwright;

namespace Acme.Hello;

#region plugin
public sealed class HelloPlugin : IPlugin
{
    public void Configure(PluginContext context)
    {
        context.Builder.Services.AddHostedService<HelloService>();
    }
}

public sealed class HelloService(ILogger<HelloService> logger) : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Hello from a verified plugin!");
        return Task.CompletedTask;
    }
}
#endregion
