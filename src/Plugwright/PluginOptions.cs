namespace Plugwright;

/// <summary>
/// Options for the plugin system, available as <c>builder.Plugins.Options</c>.
/// </summary>
public sealed class PluginOptions
{
    /// <summary>
    /// Gets or sets what happens when a plugin cannot be loaded or configured.
    /// The default is <see cref="PluginFailureMode.FailFast"/>.
    /// </summary>
    /// <example>
    /// <code language="csharp">
    /// builder.Plugins.Options.FailureMode = PluginFailureMode.SkipAndLog;
    /// </code>
    /// </example>
    public PluginFailureMode FailureMode { get; set; } = PluginFailureMode.FailFast;
}
