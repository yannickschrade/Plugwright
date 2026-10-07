using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Plugwright;

/// <summary>
/// Adds phase 2 of the plugin system, <c>app.UsePlugins()</c>, to every <see cref="IHost"/>.
/// </summary>
/// <example>
/// <code language="csharp">
/// builder.ApplyPlugins();   // phase 1, before Build()
/// var app = builder.Build();
/// app.UsePlugins();         // phase 2, after Build()
///
/// await app.RunAsync();
/// </code>
/// </example>
public static class PluginHostExtensions
{
    extension(IHost app)
    {
        /// <summary>
        /// Phase 2: writes the log entries from phase 1 and lets the plugin handlers work with the built host.
        /// Call it exactly once, after <c>Build()</c> and before the host runs.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// <c>builder.ApplyPlugins()</c> was not called before <c>Build()</c>, or this method has already been called.
        /// </exception>
        /// <exception cref="PluginException">A plugin handler failed.</exception>
        public void UsePlugins()
        {
            ArgumentNullException.ThrowIfNull(app);

            var system = app.Services.GetService<PluginSystem>()
                ?? throw new InvalidOperationException(
                    "app.UsePlugins() requires builder.ApplyPlugins() to be called before builder.Build().");
            system.Use(app);
        }
    }
}
