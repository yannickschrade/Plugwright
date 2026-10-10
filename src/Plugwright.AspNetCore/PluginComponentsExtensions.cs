using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Plugwright;

/// <summary>
/// Adds <c>AddPluginAssemblies(app)</c> to <see cref="RazorComponentsEndpointConventionBuilder"/>.
/// </summary>
public static class PluginComponentsExtensions
{
    extension(RazorComponentsEndpointConventionBuilder builder)
    {
        /// <summary>
        /// Adds the assemblies of the active Blazor plugins as additional assemblies, so that their routable pages
        /// are mapped as endpoints. Requires <see cref="BlazorPluginHandler"/>.
        /// </summary>
        /// <remarks>
        /// The application's own assembly is left out, because <c>MapRazorComponents</c> already knows it. Also pass
        /// <see cref="IPluginComponentAssemblies.Assemblies"/> to the router's <c>AdditionalAssemblies</c>.
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

            return builder;
        }
    }
}
