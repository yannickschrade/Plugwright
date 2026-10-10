namespace Plugwright;

/// <summary>
/// How the host application treats one plugin: whether it is required, and what happens when it fails.
/// Set with <c>builder.Plugins.Add&lt;T&gt;(policy =&gt; …)</c>, <c>builder.Plugins.Configure(pluginId, …)</c> or
/// <c>builder.Plugins.Require(pluginId)</c>.
/// </summary>
/// <remarks>
/// Only code sets a policy, never configuration: a file that someone can write must not turn off a required plugin.
/// </remarks>
public sealed class PluginPolicy
{
    /// <summary>
    /// Gets or sets whether the application cannot run without this plugin. The default is <see langword="false"/>.
    /// </summary>
    /// <remarks>
    /// A required plugin that is missing, turned off, depends on a plugin that is not available, fails verification or
    /// throws while it is configured or started stops the application with a <see cref="PluginException"/>, whatever
    /// the failure mode. The exception names all required plugins with problems at once.
    /// </remarks>
    public bool Required { get; set; }

    /// <summary>
    /// Gets or sets what happens when this plugin fails, or <see langword="null"/> to use
    /// <see cref="PluginOptions.FailureMode"/>. Has no effect on a required plugin.
    /// </summary>
    public PluginFailureMode? FailureMode { get; set; }
}
