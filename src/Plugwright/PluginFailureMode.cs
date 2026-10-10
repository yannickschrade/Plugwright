namespace Plugwright;

/// <summary>
/// Defines what happens when a plugin cannot be loaded or configured.
/// </summary>
public enum PluginFailureMode
{
    /// <summary>
    /// Throw a <see cref="PluginException"/> and stop the application. This is the default.
    /// </summary>
    FailFast,

    /// <summary>
    /// Skip the plugin, log an error and continue with the other plugins.
    /// </summary>
    /// <remarks>
    /// Applies to exceptions in <see cref="IPlugin.Configure"/> (or in a handler's <see cref="IPluginHandler.Apply"/>),
    /// to plugin types without a handler and to plugins that cannot be created.
    /// Services that a plugin type registered before it failed are removed again. Configuration sources, builder
    /// properties and <c>ConfigureContainer</c> callbacks it added stay. Set the mode for single plugins with
    /// <see cref="PluginPolicy.FailureMode"/>; required plugins always stop the application.
    /// </remarks>
    SkipAndLog,
}
