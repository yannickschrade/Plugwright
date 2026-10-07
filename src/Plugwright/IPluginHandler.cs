using Microsoft.Extensions.Hosting;

namespace Plugwright;

/// <summary>
/// Decides what happens with a plugin type in both phases of the plugin system.
/// </summary>
/// <remarks>
/// <para>
/// <c>builder.Plugins</c> only records plugin types. Handlers give them meaning. The built-in handler processes
/// every type that implements <see cref="IPlugin"/> and calls <see cref="IPlugin.Configure"/>. Register your own
/// handlers with <c>builder.PluginHandlers.Add&lt;T&gt;()</c> to support other kinds of plugins.
/// </para>
/// <para>
/// For a handler that processes one plugin interface or base class, derive from <see cref="PluginHandler{TPlugin}"/>
/// instead of implementing this interface directly.
/// </para>
/// <para>
/// Handlers are created with their parameterless constructor, not resolved from dependency injection, because the
/// service container does not exist yet in phase 1. All handlers that can handle a type process it, in registration
/// order, with the built-in handler first. All of them share the same plugin instance.
/// </para>
/// </remarks>
/// <example>
/// A handler for plugins that contribute menu entries:
/// <code language="csharp">
/// public interface IMenuPlugin
/// {
///     IEnumerable&lt;MenuItem&gt; GetMenuItems();
/// }
///
/// public sealed class MenuPluginHandler : IPluginHandler
/// {
///     public bool CanHandle(Type pluginType) =&gt; typeof(IMenuPlugin).IsAssignableFrom(pluginType);
///
///     public void Apply(PluginDescriptor plugin, IHostApplicationBuilder builder)
///     {
///         foreach (var item in ((IMenuPlugin)plugin.Instance).GetMenuItems())
///         {
///             builder.Services.AddSingleton(item);
///         }
///     }
/// }
///
/// builder.PluginHandlers.Add&lt;MenuPluginHandler&gt;();
/// </code>
/// </example>
public interface IPluginHandler
{
    /// <summary>
    /// Returns whether this handler processes the given plugin type.
    /// </summary>
    /// <param name="pluginType">The plugin type.</param>
    /// <returns><see langword="true"/> if the handler processes the type; otherwise <see langword="false"/>.</returns>
    bool CanHandle(Type pluginType);

    /// <summary>
    /// Phase 1: processes the plugin before the host is built, called by <c>builder.ApplyPlugins()</c>.
    /// </summary>
    /// <param name="plugin">The plugin to process.</param>
    /// <param name="builder">The host application builder.</param>
    void Apply(PluginDescriptor plugin, IHostApplicationBuilder builder);

    /// <summary>
    /// Phase 2: processes the plugin after the host is built, called by <c>app.UsePlugins()</c>.
    /// The default implementation does nothing.
    /// </summary>
    /// <param name="plugin">The plugin to process.</param>
    /// <param name="app">The built host.</param>
    void Use(PluginDescriptor plugin, IHost app)
    {
    }
}
