namespace Plugwright;

/// <summary>
/// Identity and version of a plugin.
/// </summary>
/// <remarks>
/// Metadata belongs to the plugin assembly (or package), not to a single plugin class.
/// All plugin classes in one assembly share the same metadata. For plugins added in code or loaded from a
/// directory, <see cref="Id"/> is the assembly name and <see cref="Version"/> is the <c>&lt;Version&gt;</c> of the
/// project, a semantic version such as <c>1.2.0</c> or <c>2.0.0-beta.1</c>.
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
    /// Gets the plugin version, a semantic version such as <c>1.2.0</c>.
    /// </summary>
    public string Version { get; }

    /// <summary>
    /// Gets the description of the plugin, or <see langword="null"/> if it has none.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Gets the authors of the plugin, or <see langword="null"/> if unknown.
    /// </summary>
    public string? Authors { get; init; }

    /// <summary>
    /// Gets the plugins this plugin depends on. Empty if it has no dependencies.
    /// </summary>
    public IReadOnlyList<PluginDependency> Dependencies { get; init; } = [];

    /// <summary>
    /// Returns the ID and version, for example <c>Acme.Heartbeat 1.2.0</c>.
    /// </summary>
    /// <returns>The ID and version, separated by a space.</returns>
    public override string ToString() => $"{Id} {Version}";
}
