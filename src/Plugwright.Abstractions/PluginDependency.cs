namespace Plugwright;

/// <summary>
/// A dependency of a plugin on another plugin, as part of <see cref="PluginMetadata.Dependencies"/>.
/// </summary>
public sealed class PluginDependency
{
    /// <summary>
    /// Creates a plugin dependency.
    /// </summary>
    /// <param name="pluginId">The ID of the plugin depended on.</param>
    /// <param name="minVersion">The minimum version, or <see langword="null"/> for any version.</param>
    public PluginDependency(string pluginId, string? minVersion = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);
        PluginId = pluginId;
        MinVersion = minVersion;
    }

    /// <summary>
    /// Gets the ID of the plugin depended on.
    /// </summary>
    public string PluginId { get; }

    /// <summary>
    /// Gets the minimum version of the plugin depended on, or <see langword="null"/> for any version.
    /// </summary>
    public string? MinVersion { get; }

    /// <summary>
    /// Returns the dependency, for example <c>Acme.Logging &gt;= 1.2.0</c>.
    /// </summary>
    /// <returns>The plugin ID and, if set, the minimum version.</returns>
    public override string ToString() => MinVersion is null ? PluginId : $"{PluginId} >= {MinVersion}";
}
