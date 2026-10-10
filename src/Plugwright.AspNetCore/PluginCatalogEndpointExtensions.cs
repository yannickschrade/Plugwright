using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Plugwright.AspNetCore;

namespace Plugwright;

/// <summary>
/// Adds <c>MapPluginCatalog()</c> to <see cref="IEndpointRouteBuilder"/>.
/// </summary>
public static class PluginCatalogEndpointExtensions
{
    extension(IEndpointRouteBuilder endpoints)
    {
        /// <summary>
        /// Maps a read-only endpoint that returns the plugin catalog as JSON: a list of <see cref="PluginStatus"/>
        /// with ID, version, state and reason of every plugin. Other hosts use it to learn which plugins run here.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The endpoint is not mapped automatically, and it is open to everyone who can reach the application unless
        /// you protect it, for example with <c>RequireAuthorization()</c>. It never changes anything.
        /// </para>
        /// <para>
        /// Exception messages can reveal details of the application, so they are left out unless
        /// <paramref name="includeErrorMessages"/> is <see langword="true"/>. Stack traces are never included.
        /// </para>
        /// </remarks>
        /// <param name="pattern">The route of the endpoint.</param>
        /// <param name="includeErrorMessages">Whether to include <see cref="PluginStatus.ErrorMessage"/>.</param>
        /// <returns>The endpoint's builder, for example to require authorization.</returns>
        /// <exception cref="InvalidOperationException"><c>builder.ApplyPlugins()</c> was not called.</exception>
        /// <example>
        /// <code language="csharp">
        /// app.MapPluginCatalog()
        ///    .RequireAuthorization();
        /// </code>
        /// </example>
        public RouteHandlerBuilder MapPluginCatalog(string pattern = "/_plugwright/plugins", bool includeErrorMessages = false)
        {
            ArgumentNullException.ThrowIfNull(endpoints);
            ArgumentException.ThrowIfNullOrWhiteSpace(pattern);

            var catalog = endpoints.ServiceProvider.GetService<IPluginCatalog>()
                ?? throw new InvalidOperationException(
                    "MapPluginCatalog() requires the plugin catalog. Call builder.ApplyPlugins() before builder.Build().");

            return endpoints.MapGet(pattern, () => catalog.Plugins.Select(plugin => new PluginStatus
            {
                Id = plugin.Id,
                Version = plugin.Version,
                State = plugin.State,
                Reason = plugin.Reason,
                ErrorMessage = includeErrorMessages ? plugin.ErrorMessage : null,
            }).ToArray());
        }
    }
}
