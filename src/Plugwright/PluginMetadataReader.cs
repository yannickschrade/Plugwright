using System.Reflection;

namespace Plugwright;

/// <summary>
/// What a plugin assembly declares about itself, read from its metadata.
/// </summary>
/// <param name="Metadata">ID, version, description, authors and the dependencies declared with attributes.</param>
/// <param name="AssemblyVersion">The assembly version, used to check references from other plugins.</param>
/// <param name="References">The assemblies this assembly references.</param>
internal sealed record PluginAssemblyInfo(PluginMetadata Metadata, Version AssemblyVersion, IReadOnlyList<AssemblyName> References);

/// <summary>
/// Reads plugin metadata from an assembly. Works on assemblies loaded for execution and on assemblies
/// in a <see cref="MetadataLoadContext"/>, because it only uses <see cref="CustomAttributeData"/>.
/// </summary>
internal static class PluginMetadataReader
{
    private const string InformationalVersionAttribute = "System.Reflection.AssemblyInformationalVersionAttribute";
    private const string DescriptionAttribute = "System.Reflection.AssemblyDescriptionAttribute";
    private const string CompanyAttribute = "System.Reflection.AssemblyCompanyAttribute";
    private const string PluginAttributeName = "Plugwright.PluginAttribute";
    private const string PluginDependencyAttributeName = "Plugwright.PluginDependencyAttribute";

    /// <summary>
    /// Reads a plugin assembly in its own folder without loading it for execution. No plugin code runs.
    /// </summary>
    /// <param name="mainAssemblyPath">The main assembly of the plugin folder.</param>
    /// <param name="verifiedContent">The verified content of the main assembly, or <see langword="null"/> to read it from disk.</param>
    public static PluginAssemblyInfo ReadWithoutLoading(string mainAssemblyPath, byte[]? verifiedContent)
    {
        // The host's assemblies and the framework, plus everything in the plugin folder.
        var hostAssemblies = ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);
        var pluginAssemblies = Directory.GetFiles(Path.GetDirectoryName(mainAssemblyPath)!, "*.dll");

        using var context = new MetadataLoadContext(new PathAssemblyResolver([.. hostAssemblies, .. pluginAssemblies]));
        return Read(verifiedContent is null ? context.LoadFromAssemblyPath(mainAssemblyPath) : context.LoadFromByteArray(verifiedContent));
    }

    public static PluginAssemblyInfo Read(Assembly assembly)
    {
        var name = assembly.GetName();
        var id = name.Name!;
        var assemblyVersion = name.Version ?? new Version(0, 0, 0, 0);
        var attributes = assembly.GetCustomAttributesData();
        var pluginAttribute = attributes.FirstOrDefault(attribute => attribute.AttributeType.FullName == PluginAttributeName);

        var version = WithoutBuildMetadata(ConstructorArgument(attributes, InformationalVersionAttribute))
            ?? (assemblyVersion.Revision == 0 ? assemblyVersion.ToString(3) : assemblyVersion.ToString());

        // Without <Authors> or <Company>, the .NET SDK sets Company to the assembly name. That counts as not set.
        var company = ConstructorArgument(attributes, CompanyAttribute);
        if (company == id)
        {
            company = null;
        }

        var dependencies = attributes
            .Where(attribute => attribute.AttributeType.FullName == PluginDependencyAttributeName)
            .Select(attribute => new PluginDependency(
                (string)attribute.ConstructorArguments[0].Value!,
                NamedArgument(attribute, nameof(PluginDependencyAttribute.MinVersion))))
            .ToList();

        var metadata = new PluginMetadata(id, version)
        {
            Description = ConstructorArgument(attributes, DescriptionAttribute)
                ?? NamedArgument(pluginAttribute, nameof(PluginAttribute.Description)),
            Authors = company ?? NamedArgument(pluginAttribute, nameof(PluginAttribute.Authors)),
            Dependencies = dependencies,
        };

        return new PluginAssemblyInfo(metadata, assemblyVersion, assembly.GetReferencedAssemblies());
    }

    private static string? ConstructorArgument(IEnumerable<CustomAttributeData> attributes, string attributeName)
    {
        var attribute = attributes.FirstOrDefault(attribute => attribute.AttributeType.FullName == attributeName);
        return NullIfEmpty(attribute?.ConstructorArguments.FirstOrDefault().Value as string);
    }

    private static string? NamedArgument(CustomAttributeData? attribute, string memberName)
    {
        foreach (var argument in attribute?.NamedArguments ?? [])
        {
            if (argument.MemberName == memberName)
            {
                return NullIfEmpty(argument.TypedValue.Value as string);
            }
        }

        return null;
    }

    private static string? WithoutBuildMetadata(string? version) => NullIfEmpty(version?.Split('+', 2)[0]);

    private static string? NullIfEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
}
