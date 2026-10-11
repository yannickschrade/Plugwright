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

    /// <summary>
    /// Gets or sets whether this plugin also runs without the other plugin. The default is <see langword="false"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// If the other plugin is present, an optional dependency works like a required one: the other plugin is processed
    /// first, and both use the same types. If it is missing, turned off or fails, this plugin is loaded anyway. Ask
    /// <see cref="PluginContext.IsPluginLoaded"/> in <see cref="IPlugin.Configure"/>, or <see cref="IPluginCatalog"/> at
    /// runtime, and touch the other plugin's types only if it is loaded.
    /// </para>
    /// <para>
    /// A version that is too old is an error, as for a required dependency. A reference to the other plugin's assembly
    /// becomes optional through this attribute. In a NuGet package, declare optional dependencies only with this
    /// attribute: a package dependency in the <c>.nuspec</c> is always required.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code language="csharp">
    /// [assembly: PluginDependency("Acme.Logging", MinVersion = "1.2.0", IsOptional = true)]
    /// </code>
    /// </example>
    public bool IsOptional { get; set; }
}
