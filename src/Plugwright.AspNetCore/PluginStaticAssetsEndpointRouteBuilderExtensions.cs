using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.StaticAssets;
using Microsoft.Extensions.DependencyInjection;
using Plugwright.AspNetCore;

namespace Plugwright;

/// <summary>
/// Adds <c>MapPluginStaticAssets()</c> to <see cref="IEndpointRouteBuilder"/>.
/// </summary>
public static class PluginStaticAssetsEndpointRouteBuilderExtensions
{
    extension(IEndpointRouteBuilder endpoints)
    {
        /// <summary>
        /// Serves the static web assets of plugins loaded from directories and packages, like <c>MapStaticAssets()</c>
        /// serves those of the application: with compression, ETags and fingerprinted routes.
        /// </summary>
        /// <remarks>
        /// <para>
        /// A plugin's own assets are served below <c>_content/{PluginId}/</c>, as for a project reference; assets of
        /// component libraries the plugin uses below their own <c>_content/</c> path. Routes the application already
        /// serves are left to it. Plugins added in code or referenced by the application need no call: their assets are
        /// part of the application's own.
        /// </para>
        /// <para>
        /// Plugin folders must contain the output of <c>dotnet publish</c>; the output of <c>dotnet build</c> lacks the
        /// asset files and is skipped with a warning. Include the scoped CSS of plugins with
        /// <see cref="PluginStyleSheets"/>. <c>.AddPluginAssemblies(app)</c> makes the assets known to <c>@Assets</c> and
        /// <c>ImportMap</c>.
        /// </para>
        /// <para>
        /// The call replaces <c>IWebHostEnvironment.WebRootFileProvider</c> with one that also contains the plugins'
        /// assets, below <c>_plugwright/</c>.
        /// </para>
        /// </remarks>
        /// <param name="configure">Changes the options, for example where the assets are served from.</param>
        /// <returns>A builder to add conventions to the endpoints, for example authorization.</returns>
        /// <exception cref="InvalidOperationException">
        /// <c>builder.ApplyPlugins()</c> was not called, or this method has already been called.
        /// </exception>
        /// <example>
        /// <code language="csharp">
        /// app.UsePlugins();
        /// app.MapStaticAssets();
        /// app.MapPluginStaticAssets();
        /// </code>
        /// </example>
        public StaticAssetsEndpointConventionBuilder MapPluginStaticAssets(Action<PluginStaticAssetsOptions>? configure = null)
        {
            ArgumentNullException.ThrowIfNull(endpoints);

            var options = new PluginStaticAssetsOptions();
            configure?.Invoke(options);
            var staticAssets = endpoints.ServiceProvider.GetService<PluginStaticAssets>() ?? new PluginStaticAssets();
            return staticAssets.Map(endpoints, options);
        }
    }
}
