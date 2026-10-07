using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Plugwright;

namespace FailureHandling;

#region plugins
public sealed class WorkingPlugin : IPlugin
{
    public void Configure(PluginContext context)
    {
        context.Builder.Services.AddHostedService<GreetingService>();
    }
}

public sealed class BrokenPlugin : IPlugin
{
    public void Configure(PluginContext context)
    {
        throw new InvalidOperationException("The database connection string is missing.");
    }
}
#endregion

public sealed class GreetingService(IHostApplicationLifetime lifetime) : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Console.WriteLine("WorkingPlugin is running.");
        lifetime.StopApplication();
        return Task.CompletedTask;
    }
}
