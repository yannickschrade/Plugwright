using System.Reflection;

namespace Plugwright;

/// <summary>
/// One plugin assembly with all its plugin types. Identity, version and dependencies belong to the unit;
/// all its plugin types are loaded together.
/// </summary>
internal sealed class PluginUnit
{
    public const string CodeSource = "added in code";

    private PluginUnit(PluginAssemblyInfo info, string source, string kind)
    {
        Info = info;
        Source = source;
        Kind = kind;
        Metadata = info.Metadata;
    }

    /// <summary>
    /// Gets what the assembly declares about itself.
    /// </summary>
    public PluginAssemblyInfo Info { get; }

    public string Id => Info.Metadata.Id;

    /// <summary>
    /// Gets where the unit comes from: <see cref="CodeSource"/>, the plugin folder or the package file.
    /// </summary>
    public string Source { get; }

    /// <summary>
    /// Gets what kind of source the unit comes from, for messages: <c>folder</c> or <c>package</c>.
    /// </summary>
    public string Kind { get; }

    public bool IsInCode => Source == CodeSource;

    /// <summary>
    /// Gets the plugin's assemblies to load; <see langword="null"/> for plugins added in code, which are already loaded.
    /// </summary>
    public PluginAssemblies? Assemblies { get; private init; }

    /// <summary>
    /// Gets or sets the loaded assembly. Set from the start for plugins added in code, after loading for plugin folders.
    /// </summary>
    public Assembly? Assembly { get; set; }

    /// <summary>
    /// Gets the plugin types of the unit. Known from the start for plugins added in code, found after loading for plugin folders.
    /// </summary>
    public List<Type> PluginTypes { get; } = [];

    /// <summary>
    /// Gets or sets the final metadata, including dependencies found through assembly references.
    /// </summary>
    public PluginMetadata Metadata { get; set; }

    /// <summary>
    /// Gets the units this unit depends on.
    /// </summary>
    public List<PluginUnit> DependsOn { get; } = [];

    /// <summary>
    /// Gets or sets whether the unit or one of its plugin types failed. Units depending on a failed unit are skipped.
    /// </summary>
    public bool Failed { get; set; }

    public static PluginUnit FromCode(Assembly assembly) =>
        new(PluginMetadataReader.Read(assembly), CodeSource, "code") { Assembly = assembly };

    public static PluginUnit FromFolder(PluginAssemblyInfo info, string folder, PluginAssemblies assemblies) =>
        new(info, folder, "folder") { Assemblies = assemblies };

    /// <summary>
    /// Creates a unit for a plugin from another source, such as a package.
    /// </summary>
    /// <param name="info">What the plugin declares about itself.</param>
    /// <param name="source">Where the plugin comes from, for example the package file.</param>
    /// <param name="kind">What kind of source it is, for messages, for example <c>package</c>.</param>
    /// <param name="assemblies">The plugin's assemblies.</param>
    public static PluginUnit FromSource(PluginAssemblyInfo info, string source, string kind, PluginAssemblies assemblies) =>
        new(info, source, kind) { Assemblies = assemblies };

    public string Describe() => IsInCode ? $"by '{PluginTypes[0]}' added in code" : $"by {Kind} '{Source}'";
}
