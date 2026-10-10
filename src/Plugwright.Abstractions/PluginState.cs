using System.Text.Json.Serialization;

namespace Plugwright;

/// <summary>
/// The state of a plugin after startup, as part of its <see cref="PluginInfo"/>.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<PluginState>))]
public enum PluginState
{
    /// <summary>
    /// The plugin is loaded and all its handlers ran.
    /// </summary>
    Loaded,

    /// <summary>
    /// The plugin is turned off in configuration (<c>Plugwright:Plugins:{PluginId}:Enabled</c>) and was not loaded.
    /// </summary>
    Disabled,

    /// <summary>
    /// The plugin was not loaded, or its startup tasks did not run, because a plugin it depends on is missing, has the
    /// wrong version or is not loaded itself. <see cref="PluginInfo.Reason"/> names the dependency.
    /// </summary>
    DependencyUnavailable,

    /// <summary>
    /// The plugin failed: it could not be read or loaded, or it threw while it was configured or started.
    /// </summary>
    Failed,

    /// <summary>
    /// The plugin did not pass verification and was not loaded.
    /// </summary>
    VerificationFailed,

    /// <summary>
    /// No registered plugin handler can handle the plugin's types.
    /// </summary>
    NoHandler,
}
