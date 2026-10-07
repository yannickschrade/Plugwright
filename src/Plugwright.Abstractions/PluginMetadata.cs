namespace Plugwright;

/// <summary>
/// Identity and version of a plugin.
/// </summary>
/// <remarks>
/// Identity and version belong to the plugin assembly (or package), not to a single plugin class.
/// All plugin classes in one assembly share the same metadata. For plugins added in code or loaded from a
/// directory, <see cref="Id"/> is the assembly name and <see cref="Version"/> is the assembly version.
/// </remarks>
public sealed class PluginMetadata
{
    /// <summary>
    /// Creates plugin metadata.
    /// </summary>
    /// <param name="id">The plugin ID, for example <c>Acme.Heartbeat</c>.</param>
    /// <param name="version">The plugin version, for example <c>1.2.0</c>.</param>
    public PluginMetadata(string id, string version)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(version);

        Id = id;
        Version = version;
    }

    /// <summary>
    /// Gets the plugin ID. It names the plugin's configuration section, <c>Plugwright:Plugins:{Id}</c>.
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// Gets the plugin version.
    /// </summary>
    public string Version { get; }

    /// <summary>
    /// Returns the ID and version, for example <c>Acme.Heartbeat 1.2.0</c>.
    /// </summary>
    /// <returns>The ID and version, separated by a space.</returns>
    public override string ToString() => $"{Id} {Version}";
}
