using Microsoft.Extensions.Hosting;

namespace Plugwright;

/// <summary>
/// Adds the plugin system to every <see cref="IHostApplicationBuilder"/>: <c>builder.Plugins</c>,
/// <c>builder.PluginHandlers</c> and <c>builder.ApplyPlugins()</c>.
/// </summary>
/// <remarks>
/// The plugin system runs in two explicit phases:
/// <c>builder.ApplyPlugins()</c> before <c>builder.Build()</c>, and <c>app.UsePlugins()</c> after it.
/// </remarks>
/// <example>
/// <code language="csharp">
/// var builder = Host.CreateApplicationBuilder(args);
/// builder.Plugins.Add&lt;HeartbeatPlugin&gt;();
///
/// builder.ApplyPlugins();   // phase 1, before Build()
/// var app = builder.Build();
/// app.UsePlugins();         // phase 2, after Build()
///
/// await app.RunAsync();
/// </code>
/// </example>
public static class PluginHostApplicationBuilderExtensions
{
    extension(IHostApplicationBuilder builder)
    {
        /// <summary>
        /// Gets the plugins of the application. Add plugins here before calling <c>ApplyPlugins()</c>.
        /// </summary>
        public PluginCollection Plugins
        {
            get
            {
                ArgumentNullException.ThrowIfNull(builder);
                return PluginSystem.GetOrCreate(builder).Plugins;
            }
        }

        /// <summary>
        /// Gets the plugin handlers of the application. The built-in handler for <see cref="IPlugin"/> is always registered.
        /// </summary>
        public PluginHandlerCollection PluginHandlers
        {
            get
            {
                ArgumentNullException.ThrowIfNull(builder);
                return PluginSystem.GetOrCreate(builder).Handlers;
            }
        }

        /// <summary>
        /// Phase 1: creates every plugin and lets its handlers register services. Call it exactly once, before <c>Build()</c>.
        /// </summary>
        /// <remarks>
        /// After this call, no more plugins or handlers can be added.
        /// </remarks>
        /// <exception cref="InvalidOperationException">The method has already been called.</exception>
        /// <exception cref="PluginException">
        /// A plugin failed and <see cref="PluginOptions.FailureMode"/> is <see cref="PluginFailureMode.FailFast"/>.
        /// </exception>
        public void ApplyPlugins()
        {
            ArgumentNullException.ThrowIfNull(builder);
            PluginSystem.GetOrCreate(builder).Apply(builder);
        }
    }
}
