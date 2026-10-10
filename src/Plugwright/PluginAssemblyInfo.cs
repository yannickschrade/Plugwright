using System.Reflection;

namespace Plugwright;

/// <summary>
/// What a plugin assembly declares about itself, read from its metadata.
/// </summary>
/// <param name="Metadata">ID, version, description, authors and the dependencies declared with attributes.</param>
/// <param name="AssemblyVersion">The assembly version, used to check references from other plugins.</param>
/// <param name="References">The assemblies this assembly references.</param>
internal sealed record PluginAssemblyInfo(PluginMetadata Metadata, Version AssemblyVersion, IReadOnlyList<AssemblyName> References)
{
    /// <summary>
    /// Gets whether an available version satisfies a dependency with a <see cref="PluginDependency.VersionRange"/>.
    /// Set by sources whose dependencies have version ranges, such as NuGet packages.
    /// </summary>
    public Func<PluginDependency, string, bool>? SatisfiesVersionRange { get; init; }
}
