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
    /// Gets the units this unit depends on optionally and that are present: they come first and share their types, but
    /// this unit does not need them.
    /// </summary>
    public List<PluginUnit> OptionallyDependsOn { get; } = [];

    /// <summary>
    /// Gets the static web assets the plugin ships: of its folder, or of its package and its library packages.
    /// </summary>
    public List<PluginStaticWebAssets> StaticWebAssets { get; } = [];

    /// <summary>
    /// Gets or sets the plugin's state, or <see langword="null"/> while it is still being processed.
    /// </summary>
    public PluginState? State { get; set; }

    /// <summary>
    /// Gets whether the plugin cannot be used: it is in any state other than loaded.
    /// </summary>
    public bool Failed => State is { } state && state != PluginState.Loaded;

    /// <summary>
    /// Gets or sets why the plugin is not loaded.
    /// </summary>
    public string? Reason { get; set; }

    /// <summary>
    /// Gets or sets the message of the exception that made the plugin fail.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Gets the names of the handlers that processed the plugin's types.
    /// </summary>
    public List<string> AppliedHandlers { get; } = [];

    /// <summary>
    /// Returns the plugin's catalog entry.
    /// </summary>
    public PluginInfo ToInfo() => new()
    {
        Id = Id,
        Version = MetadataKnown ? Metadata.Version : null,
        Description = Metadata.Description,
        Source = Source,
        State = State ?? PluginState.Loaded,
        Reason = Reason,
        ErrorMessage = ErrorMessage,
        IsRequired = IsRequired,
        Dependencies = [.. Metadata.Dependencies.Where(dependency => !dependency.IsOptional).Select(dependency => dependency.PluginId)],
        OptionalDependencies = [.. Metadata.Dependencies.Where(dependency => dependency.IsOptional).Select(dependency => dependency.PluginId)],
        AppliedHandlers = [.. AppliedHandlers],
    };

    /// <summary>
    /// Creates a unit for a plugin that is turned off in configuration. Its metadata is only known when it could be
    /// read safely, that is without verifiers or from a package's manifest.
    /// </summary>
    public static PluginUnit Disabled(string id, string source, string kind, PluginMetadata? metadata)
    {
        var known = metadata is not null;
        metadata ??= new PluginMetadata(id, "0.0.0");
        return new PluginUnit(new PluginAssemblyInfo(metadata, new Version(0, 0), []), source, kind)
        {
            MetadataKnown = known,
            State = PluginState.Disabled,
            Reason = PluginConfiguration.DisabledReason(id),
        };
    }

    /// <summary>
    /// Gets whether the plugin's metadata was read. Only plugins that are turned off can lack it.
    /// </summary>
    public bool MetadataKnown { get; private init; } = true;

    /// <summary>
    /// Gets or sets whether the plugin is not loaded only because a plugin it depends on, directly or transitively,
    /// is turned off. That is no failure: the failure mode does not apply.
    /// </summary>
    public bool UnavailableBecauseDisabled { get; set; }

    /// <summary>
    /// Gets or sets whether the host application requires the plugin.
    /// </summary>
    public bool IsRequired { get; set; }

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
