using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Plugwright;

/// <summary>
/// Runs the plugins' startup tasks in <see cref="IHostedLifecycleService.StartingAsync"/>, which the host calls before
/// the <c>StartAsync</c> of any hosted service, the web server included. Registered first among the hosted services.
/// </summary>
internal sealed class PluginStartupRunner(
    PluginSystem system,
    IEnumerable<PluginStartupRegistration> registrations,
    IServiceScopeFactory scopeFactory,
    ILoggerFactory loggerFactory) : IHostedLifecycleService
{
    private static readonly ActivitySource s_activitySource = new(PluginMetrics.MeterName);

    public async Task StartingAsync(CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger(PluginSystem.LoggerCategory);

        // Registrations are in the order the plugins were configured: dependency order, then the order of AddStartup.
        foreach (var registration in registrations)
        {
            var taskName = registration.StartupType.FullName ?? registration.StartupType.Name;
            var plugin = system.Catalog.Find(registration.PluginId);
            if (plugin is null || plugin.State != PluginState.Loaded)
            {
                PluginLog.PluginStartupSkipped(logger, registration.PluginId, taskName, plugin?.Reason ?? "The plugin is not loaded.");
                continue;
            }

            using var activity = s_activitySource.StartActivity("Plugwright.Startup");
            activity?.SetTag("plugwright.plugin.id", registration.PluginId);
            activity?.SetTag("plugwright.startup.type", taskName);
            var stopwatch = Stopwatch.StartNew();
            try
            {
                var scope = scopeFactory.CreateAsyncScope();
                await using var _ = scope.ConfigureAwait(false);
                var task = (IPluginStartup)scope.ServiceProvider.GetRequiredService(registration.StartupType);
                await task.StartAsync(cancellationToken).ConfigureAwait(false);
                PluginLog.PluginStartupCompleted(logger, registration.PluginId, taskName, stopwatch.Elapsed.TotalMilliseconds);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                activity?.SetStatus(ActivityStatusCode.Error, exception.Message);
                PluginLog.PluginStartupFailed(logger, exception, registration.PluginId, taskName, stopwatch.Elapsed.TotalMilliseconds);
                system.FailStartup(plugin, taskName, exception);
            }
        }
    }

    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StartedAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StoppingAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StoppedAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
