using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Plugwright;

/// <summary>
/// Adds <c>AddPlugins()</c> to <see cref="IHealthChecksBuilder"/>: a health check for the plugins of the application.
/// </summary>
public static class PluginHealthChecksBuilderExtensions
{
    extension(IHealthChecksBuilder builder)
    {
        /// <summary>
        /// Adds a health check that reports plugins that do not run because of an error.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The check is healthy while every plugin that is turned on runs. It reports <paramref name="failureStatus"/>
        /// for plugins that are <see cref="PluginState.Failed"/>, <see cref="PluginState.VerificationFailed"/> or
        /// <see cref="PluginState.NoHandler"/>, and for plugins that are <see cref="PluginState.DependencyUnavailable"/>
        /// because of such an error, for example a missing dependency or a failed startup task. Plugins that are turned
        /// off, and plugins that miss only a plugin that is turned off, are healthy: turning a plugin off is a decision,
        /// not an error. The result's data holds the state of each reported plugin.
        /// </para>
        /// <para>
        /// The check covers what Plugwright knows: whether plugins started. Whether a running plugin works, for example
        /// whether its database is reachable, only the plugin knows; let it register its own health check in
        /// <see cref="IPlugin.Configure"/>. A plugin that is turned off or fails registers none.
        /// </para>
        /// <para>
        /// Required plugins need no health check: a problem with one stops the application.
        /// </para>
        /// </remarks>
        /// <param name="name">The name of the check.</param>
        /// <param name="failureStatus">The status to report when plugins do not run because of an error.</param>
        /// <param name="tags">Tags to filter health checks by, for example for readiness endpoints.</param>
        /// <returns>The builder, for chaining.</returns>
        /// <example>
        /// <code language="csharp">
        /// builder.Services.AddHealthChecks().AddPlugins();
        /// </code>
        /// </example>
        public IHealthChecksBuilder AddPlugins(
            string name = "plugins", HealthStatus failureStatus = HealthStatus.Degraded, IEnumerable<string>? tags = null)
        {
            ArgumentNullException.ThrowIfNull(builder);
            ArgumentException.ThrowIfNullOrWhiteSpace(name);

            return builder.Add(new HealthCheckRegistration(
                name,
                services => new PluginHealthCheck(services.GetService<PluginSystem>(), failureStatus),
                failureStatus,
                tags));
        }
    }
}
