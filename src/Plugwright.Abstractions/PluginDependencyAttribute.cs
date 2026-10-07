namespace Plugwright;

/// <summary>
/// Declares that this plugin assembly depends on another plugin.
/// </summary>
/// <remarks>
/// <para>
/// A dependency means: the other plugin must be present, it is processed before this one in both phases,
/// and this plugin uses the same types as the other plugin.
/// </para>
/// <para>
/// A reference to another plugin's assembly is a dependency by itself; the attribute is only needed for dependencies
/// without such a reference, or to require a minimum version. If both exist, both must be satisfied.
/// </para>
/// </remarks>
/// <example>
/// <code language="csharp">
/// [assembly: PluginDependency("Acme.Logging", MinVersion = "1.2.0")]
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
public sealed class PluginDependencyAttribute : Attribute
{
    /// <summary>
    /// Declares a dependency on another plugin.
    /// </summary>
    /// <param name="pluginId">The ID of the plugin this assembly depends on.</param>
    public PluginDependencyAttribute(string pluginId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);
        PluginId = pluginId;
    }

    /// <summary>
    /// Gets the ID of the plugin this assembly depends on.
    /// </summary>
    public string PluginId { get; }

    /// <summary>
    /// Gets or sets the minimum version of the other plugin, as a semantic version such as <c>1.2.0</c>.
    /// <see langword="null"/> accepts any version.
    /// </summary>
    public string? MinVersion { get; set; }
}
