using System.Reflection;

namespace Plugwright;

/// <summary>
/// One plugin assembly with all its plugin types. Identity, version and dependencies belong to the unit;
/// all its plugin types are loaded together.
/// </summary>
internal sealed class PluginUnit
{
    public const string CodeSource = "added in code";

    private PluginUnit(PluginAssemblyInfo info, string source)
    {
        Info = info;
        Source = source;
        Metadata = info.Metadata;
    }

    /// <summary>
    /// Gets what the assembly declares about itself.
    /// </summary>
    public PluginAssemblyInfo Info { get; }

    public string Id => Info.Metadata.Id;

    /// <summary>
    /// Gets where the unit comes from: <see cref="CodeSource"/> or the plugin folder.
    /// </summary>
    public string Source { get; }

    public bool IsInCode => Source == CodeSource;

    /// <summary>
    /// Gets the main assembly path of a plugin folder; <see langword="null"/> for plugins added in code.
    /// </summary>
    public string? MainAssemblyPath { get; private init; }

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
        new(PluginMetadataReader.Read(assembly), CodeSource) { Assembly = assembly };

    public static PluginUnit FromFolder(PluginAssemblyInfo info, string folder, string mainAssemblyPath) =>
        new(info, folder) { MainAssemblyPath = mainAssemblyPath };

    public string Describe() => IsInCode ? $"by '{PluginTypes[0]}' added in code" : $"by folder '{Source}'";
}
