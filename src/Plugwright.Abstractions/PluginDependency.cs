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
    /// Gets the allowed versions in NuGet notation, such as <c>[1.2.0, 2.0.0)</c>, for dependencies declared in a NuGet
    /// package. <see langword="null"/> for dependencies declared with <see cref="PluginDependencyAttribute"/> or found
    /// through assembly references.
    /// </summary>
    public string? VersionRange { get; init; }

    /// <summary>
    /// Gets whether the plugin also runs without the plugin depended on. See <see cref="PluginDependencyAttribute.IsOptional"/>.
    /// </summary>
    public bool IsOptional { get; init; }

    /// <summary>
    /// Returns the dependency, for example <c>Acme.Logging &gt;= 1.2.0</c>, <c>Acme.Logging [1.2.0, 2.0.0)</c> or
    /// <c>Acme.Logging (optional)</c>.
    /// </summary>
    /// <returns>The plugin ID and, if set, the allowed versions and whether the dependency is optional.</returns>
    public override string ToString()
    {
        var text = (VersionRange, MinVersion) switch
        {
            ({ } range, _) => $"{PluginId} {range}",
            (null, { } minVersion) => $"{PluginId} >= {minVersion}",
            _ => PluginId,
        };
        return IsOptional ? $"{text} (optional)" : text;
    }
}
