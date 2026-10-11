using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Plugwright.AspNetCore;

namespace Plugwright;

/// <summary>
/// Adds <c>AddPluginAssemblies(app)</c> to <see cref="RazorComponentsEndpointConventionBuilder"/>.
/// </summary>
public static class PluginRazorComponentsEndpointConventionBuilderExtensions
{
    extension(RazorComponentsEndpointConventionBuilder builder)
    {
        /// <summary>
        /// Adds the assemblies of the active Blazor plugins as additional assemblies, so that their routable pages
        /// are mapped as endpoints, and makes the static web assets of plugins known to <c>@Assets</c> and
        /// <c>ImportMap</c>. Requires <see cref="BlazorPluginHandler"/>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The application's own assembly is left out, because <c>MapRazorComponents</c> already knows it. Also pass
        /// <see cref="IPluginComponentAssemblies.Assemblies"/> to the router's <c>AdditionalAssemblies</c>.
        /// </para>
        /// <para>
        /// The assets that <c>app.MapPluginStaticAssets()</c> serves are added to the application's own resource
        /// collection, so that <c>@Assets["_content/{PluginId}/…"]</c> returns the fingerprinted route and the import map
        /// contains the plugins' JavaScript modules. The order of the two calls does not matter.
        /// </para>
        /// </remarks>
        /// <param name="app">The built host, after <c>app.UsePlugins()</c>.</param>
        /// <returns>The builder, for chaining.</returns>
        /// <exception cref="InvalidOperationException"><see cref="BlazorPluginHandler"/> is not registered.</exception>
        /// <example>
        /// <code language="csharp">
        /// app.MapRazorComponents&lt;App&gt;()
        ///    .AddInteractiveServerRenderMode()
        ///    .AddPluginAssemblies(app);
        /// </code>
        /// </example>
        public RazorComponentsEndpointConventionBuilder AddPluginAssemblies(IHost app)
        {
            ArgumentNullException.ThrowIfNull(builder);
            ArgumentNullException.ThrowIfNull(app);

            var componentAssemblies = app.Services.GetService<IPluginComponentAssemblies>()
                ?? throw new InvalidOperationException(
                    "AddPluginAssemblies(app) requires the Blazor plugin handler. "
                    + "Call builder.PluginHandlers.Add<BlazorPluginHandler>() before builder.ApplyPlugins().");

            var applicationName = app.Services.GetRequiredService<IHostEnvironment>().ApplicationName;
            var assemblies = componentAssemblies.Assemblies
                .Where(assembly => assembly.GetName().Name != applicationName)
                .ToArray();

            if (assemblies.Length > 0)
            {
                builder.AddAdditionalAssemblies(assemblies);
            }

            // Runs when the endpoints are built, after the application set up its own resource collection and after
            // app.MapPluginStaticAssets(), wherever it is called.
            var staticAssets = app.Services.GetRequiredService<PluginStaticAssets>();
            ((IEndpointConventionBuilder)builder).Finally(endpoint =>
            {
                if (staticAssets.Resources.Count == 0)
                {
                    return;
                }

                var own = (IEnumerable<ResourceAsset>?)endpoint.Metadata.OfType<ResourceAssetCollection>().LastOrDefault() ?? [];
                var resources = new ResourceAssetCollection([.. own, .. staticAssets.Resources]);
                endpoint.Metadata.Add(resources);
                endpoint.Metadata.Add(ImportMapDefinition.FromResourceCollection(resources));
            });

            return builder;
        }
    }
}
