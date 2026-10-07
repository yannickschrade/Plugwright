using Microsoft.Extensions.Hosting;

namespace Plugwright;

/// <summary>
/// Base class for a plugin handler that processes every plugin implementing or deriving from <typeparamref name="TPlugin"/>.
/// </summary>
/// <remarks>
/// This is the easiest way to write a handler for one kind of plugin. It implements
/// <see cref="IPluginHandler.CanHandle"/> and passes the plugin instance already typed as <typeparamref name="TPlugin"/>.
/// Implement <see cref="IPluginHandler"/> directly when a handler needs a different rule for which types it handles.
/// </remarks>
/// <typeparam name="TPlugin">The plugin interface or base class this handler processes.</typeparam>
/// <example>
/// <code language="csharp">
/// public sealed class GreetingPluginHandler : PluginHandler&lt;IGreetingPlugin&gt;
/// {
///     protected override void Apply(IGreetingPlugin instance, PluginDescriptor plugin, IHostApplicationBuilder builder)
///     {
///         foreach (var text in instance.GetGreetings())
///         {
///             builder.Services.AddSingleton(new Greeting(text, plugin.Metadata.Id));
///         }
///     }
/// }
/// </code>
/// </example>
public abstract class PluginHandler<TPlugin> : IPluginHandler
    where TPlugin : class
{
    /// <summary>
    /// Returns whether the plugin type implements or derives from <typeparamref name="TPlugin"/>.
    /// </summary>
    /// <param name="pluginType">The plugin type.</param>
    /// <returns><see langword="true"/> if the type is assignable to <typeparamref name="TPlugin"/>; otherwise <see langword="false"/>.</returns>
    public bool CanHandle(Type pluginType) => typeof(TPlugin).IsAssignableFrom(pluginType);

    void IPluginHandler.Apply(PluginDescriptor plugin, IHostApplicationBuilder builder) =>
        Apply((TPlugin)plugin.Instance, plugin, builder);

    void IPluginHandler.Use(PluginDescriptor plugin, IHost app) =>
        Use((TPlugin)plugin.Instance, plugin, app);

    /// <summary>
    /// Phase 1: processes the plugin before the host is built, called by <c>builder.ApplyPlugins()</c>.
    /// </summary>
    /// <param name="instance">The plugin instance.</param>
    /// <param name="plugin">The plugin's descriptor with its type, metadata and context.</param>
    /// <param name="builder">The host application builder.</param>
    protected abstract void Apply(TPlugin instance, PluginDescriptor plugin, IHostApplicationBuilder builder);

    /// <summary>
    /// Phase 2: processes the plugin after the host is built, called by <c>app.UsePlugins()</c>.
    /// The default implementation does nothing.
    /// </summary>
    /// <param name="instance">The plugin instance.</param>
    /// <param name="plugin">The plugin's descriptor with its type, metadata and context.</param>
    /// <param name="app">The built host.</param>
    protected virtual void Use(TPlugin instance, PluginDescriptor plugin, IHost app)
    {
    }
}
