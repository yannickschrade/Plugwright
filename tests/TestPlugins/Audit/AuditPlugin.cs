using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Plugwright;
using Plugwright.TestPlugins.Logging;

[assembly: PluginDependency("Plugwright.TestPlugins.Logging", MinVersion = "1.0.0")]

namespace Plugwright.TestPlugins.Audit;

public sealed class AuditPlugin : IPlugin
{
    public void Configure(PluginContext context)
    {
        context.Builder.Configuration["TestPlugins:Order"] += "Audit;";
        context.Builder.Configuration["TestPlugins:Audit:Dependencies"] = string.Join(",", context.Metadata.Dependencies);
        context.Builder.Services.AddHostedService<AuditService>();
        context.AddStartup<AuditStartup>();
    }
}

public sealed class AuditStartup(IConfiguration configuration) : IPluginStartup
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        configuration["TestPlugins:Startup"] += "Audit;";
        return Task.CompletedTask;
    }
}

/// <summary>
/// Receives the LogSink registered by the Logging plugin. Resolving it only works if both plugins share the type.
/// </summary>
public sealed class AuditService(LogSink sink) : IHostedService
{
    public LogSink Sink { get; } = sink;

    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
