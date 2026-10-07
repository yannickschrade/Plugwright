using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Plugwright.TestPlugins.TextLib;

namespace Plugwright.TestPlugins.Counter;

public sealed class CounterPlugin : IPlugin
{
    public void Configure(PluginContext context)
    {
        context.Builder.Configuration["TestPlugins:Counter:TextLibVersion"] = TextInfo.Version;
        context.Builder.Services.AddHostedService<CounterService>();
    }
}

/// <summary>
/// A hosted service from a plugin, resolved by the host's container through the shared IHostedService.
/// </summary>
public sealed class CounterService : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
