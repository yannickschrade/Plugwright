using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Plugwright;

/// <summary>
/// Reports plugins that do not run because of an error. Plugins that are turned off, and plugins that only miss a
/// plugin that is turned off, are no problem.
/// </summary>
/// <param name="system">The plugin system, or <see langword="null"/> if <c>builder.ApplyPlugins()</c> was not called.</param>
/// <param name="failureStatus">The status to report for plugins that do not run because of an error.</param>
internal sealed class PluginHealthCheck(PluginSystem? system, HealthStatus failureStatus) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        if (system is null)
        {
            return Task.FromResult(new HealthCheckResult(
                failureStatus, "The plugin health check needs builder.ApplyPlugins() before builder.Build()."));
        }

        var failed = system.Catalog.Plugins.Where(system.IsNotRunningBecauseOfError).ToList();
        if (failed.Count == 0)
        {
            return Task.FromResult(HealthCheckResult.Healthy("All plugins that are turned on are running."));
        }

        var description = $"{failed.Count} plugin(s) do not run because of an error: "
            + string.Join(", ", failed.Select(plugin => $"{plugin.Id} ({plugin.State})"));
        var data = failed.ToDictionary(plugin => plugin.Id, plugin => (object)plugin.State.ToString());
        return Task.FromResult(new HealthCheckResult(failureStatus, description, data: data));
    }
}
