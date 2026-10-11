using System.Runtime.CompilerServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Plugwright;
using Plugwright.TestPlugins.Logging;

[assembly: PluginDependency("Plugwright.TestPlugins.Logging", MinVersion = "1.0.0", IsOptional = true)]

namespace Plugwright.TestPlugins.OptionalAudit;

/// <summary>
/// Uses the Logging plugin's LogSink if the Logging plugin is loaded, and runs without it otherwise.
/// </summary>
public sealed class OptionalAuditPlugin : IPlugin
{
    public void Configure(PluginContext context)
    {
        var loggingLoaded = context.IsPluginLoaded("Plugwright.TestPlugins.Logging");
        context.Builder.Configuration["TestPlugins:Order"] += "OptionalAudit;";
        context.Builder.Configuration["TestPlugins:OptionalAudit:LoggingLoaded"] = loggingLoaded.ToString();
        context.Builder.Configuration["TestPlugins:OptionalAudit:LoggingTypeAvailable"] = IsLogSinkAvailable().ToString();

        if (loggingLoaded)
        {
            context.Builder.Services.AddHostedService<OptionalAuditService>();
        }
    }

    // Touching LogSink loads the Logging assembly; in its own method, so that only this call fails without it.
    private static bool IsLogSinkAvailable()
    {
        try
        {
            return LogSinkTypeName() is not null;
        }
        catch (FileNotFoundException)
        {
            return false;
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static string? LogSinkTypeName() => typeof(LogSink).FullName;
}

/// <summary>
/// Receives the LogSink registered by the Logging plugin. Resolving it only works if both plugins share the type.
/// </summary>
public sealed class OptionalAuditService(LogSink sink) : IHostedService
{
    public LogSink Sink { get; } = sink;

    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
