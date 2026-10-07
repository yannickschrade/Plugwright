using NuGet.Packaging.Core;
using NuGet.Versioning;

namespace Plugwright;

/// <summary>
/// A folder of NuGet packages added with <see cref="PluginPackageExtensions.AddFromPackages"/>.
/// </summary>
public sealed class PluginPackageSource : PluginSource, IDiscoveringPluginSource
{
    private static readonly Lazy<HashSet<string>> s_hostAssemblies = new(() =>
        ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") ?? string.Empty)
            .Split(System.IO.Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Select(System.IO.Path.GetFileNameWithoutExtension)
            .ToHashSet(StringComparer.OrdinalIgnoreCase)!);

    internal PluginPackageSource(string path)
    {
        Path = path;
    }

    /// <summary>
    /// Gets the path as it was passed to <see cref="PluginPackageExtensions.AddFromPackages"/>.
    /// A relative path is resolved against the content root when <c>builder.ApplyPlugins()</c> runs.
    /// </summary>
    public string Path { get; }

    /// <summary>
    /// Returns a description of the source, for example <c>Packages packages</c>.
    /// </summary>
    /// <returns>A description of the source.</returns>
    public override string ToString() => $"Packages {Path}";

    IEnumerable<PluginUnit> IDiscoveringPluginSource.Discover(PluginDiscoveryContext context)
    {
        var directory = System.IO.Path.GetFullPath(Path, context.ContentRoot);
        if (!Directory.Exists(directory))
        {
            PluginLog.PluginDirectoryNotFound(context.Logger, directory);
            return [];
        }

        var packages = ReadPackages(directory, context);
        var pluginIds = packages.Where(package => package.IsPlugin).Select(package => package.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var libraries = packages.Where(package => !package.IsPlugin).ToLookup(package => package.Id, StringComparer.OrdinalIgnoreCase);

        List<PluginUnit> units = [];
        foreach (var package in packages.Where(package => package.IsPlugin))
        {
            if (CreateUnit(package, pluginIds, libraries, context) is { } unit)
            {
                units.Add(unit);
            }
        }

        return units;
    }

    /// <summary>
    /// Reads every .nupkg file once into memory and verifies it. Packages that cannot be read or fail verification are left out.
    /// </summary>
    private List<PluginPackage> ReadPackages(string directory, PluginDiscoveryContext context)
    {
        List<PluginPackage> packages = [];
        foreach (var path in Directory.EnumerateFiles(directory, "*.nupkg").Order(StringComparer.Ordinal))
        {
            var fileName = System.IO.Path.GetFileName(path);
            PluginPackage package;
            try
            {
                package = PluginPackage.Read(path, File.ReadAllBytes(path));
            }
            catch (Exception exception) when (exception is InvalidDataException or IOException)
            {
                context.Fail(fileName, path, exception.Message, exception);
                continue;
            }

            // A package is verified under its file name without .nupkg, so that versions of a library do not collide.
            if (context.VerificationEnabled
                && !context.Verify(this, System.IO.Path.GetFileNameWithoutExtension(path), path, [PluginDiscoveryContext.CreateFile(fileName, package.Content)]))
            {
                continue;
            }

            packages.Add(package);
        }

        return packages;
    }

    private static PluginUnit? CreateUnit(
        PluginPackage package,
        HashSet<string> pluginIds,
        ILookup<string, PluginPackage> libraries,
        PluginDiscoveryContext context)
    {
        if (!package.Assemblies.TryGetValue(package.Id, out var mainAssembly))
        {
            context.Fail(
                package.Id,
                package.Path,
                $"The plugin package contains no '{package.Id}.dll' for net10.0. A plugin package must contain an assembly with the package ID.",
                exception: null);
            return null;
        }

        var assemblies = new Dictionary<string, byte[]>(package.Assemblies, StringComparer.OrdinalIgnoreCase);
        var nativeLibraries = new Dictionary<string, byte[]>(package.NativeLibraries, StringComparer.Ordinal);
        List<PluginDependency> pluginDependencies = [];

        var problem = AddDependencies(package, pluginIds, libraries, assemblies, nativeLibraries, pluginDependencies, [package.Id]);
        if (problem is not null)
        {
            context.Fail(package.Id, package.Path, problem, exception: null);
            return null;
        }

        PluginAssemblyInfo assemblyInfo;
        try
        {
            assemblyInfo = PluginMetadataReader.ReadWithoutLoading(mainAssembly);
        }
        catch (Exception exception)
        {
            context.Fail(package.Id, package.Path, $"'{package.Id}.dll' could not be read: {exception.Message}", exception);
            return null;
        }

        // The package metadata wins; the assembly's metadata and attributes only fill gaps.
        var fromAssembly = assemblyInfo.Metadata;
        var metadata = new PluginMetadata(package.Id, package.Version.ToNormalizedString())
        {
            Description = package.Description ?? fromAssembly.Description,
            Authors = package.Authors ?? fromAssembly.Authors,
            Dependencies = [.. pluginDependencies, .. fromAssembly.Dependencies],
        };

        var info = assemblyInfo with { Metadata = metadata, SatisfiesVersionRange = SatisfiesVersionRange };
        return PluginUnit.FromSource(info, package.Path, "package", new PackageAssemblies(package.Id, assemblies, nativeLibraries));
    }

    /// <summary>
    /// Resolves the dependencies of a package. Plugin packages become plugin dependencies; library packages are taken
    /// from the folder, in the lowest version that matches, together with their own dependencies.
    /// Dependencies the host already provides, such as Microsoft.Extensions.*, are satisfied by the host.
    /// </summary>
    /// <returns>A description of the first problem, or <see langword="null"/>.</returns>
    private static string? AddDependencies(
        PluginPackage package,
        HashSet<string> pluginIds,
        ILookup<string, PluginPackage> libraries,
        Dictionary<string, byte[]> assemblies,
        Dictionary<string, byte[]> nativeLibraries,
        List<PluginDependency> pluginDependencies,
        HashSet<string> visited)
    {
        foreach (var dependency in package.Dependencies)
        {
            if (!visited.Add(dependency.Id))
            {
                continue;
            }

            if (pluginIds.Contains(dependency.Id))
            {
                pluginDependencies.Add(new PluginDependency(dependency.Id, dependency.VersionRange.MinVersion?.ToNormalizedString())
                {
                    VersionRange = dependency.VersionRange.ToNormalizedString(),
                });
                continue;
            }

            if (libraries[dependency.Id].Where(library => dependency.VersionRange.Satisfies(library.Version)).MinBy(library => library.Version) is { } library)
            {
                foreach (var (name, content) in library.Assemblies)
                {
                    assemblies.TryAdd(name, content);
                }

                foreach (var (name, content) in library.NativeLibraries)
                {
                    nativeLibraries.TryAdd(name, content);
                }

                if (AddDependencies(library, pluginIds, libraries, assemblies, nativeLibraries, pluginDependencies, visited) is { } problem)
                {
                    return problem;
                }

                continue;
            }

            if (s_hostAssemblies.Value.Contains(dependency.Id))
            {
                continue;
            }

            var available = libraries[dependency.Id].Select(library => library.Version.ToNormalizedString()).ToList();
            return available.Count == 0
                ? $"Package '{package}' depends on package '{dependency.Id}' {dependency.VersionRange.ToNormalizedString()}, "
                    + "which is neither in the package folder nor provided by the host."
                : $"Package '{package}' depends on package '{dependency.Id}' {dependency.VersionRange.ToNormalizedString()}, "
                    + $"but the package folder only has version {string.Join(", ", available)}.";
        }

        return null;
    }

    private static bool SatisfiesVersionRange(PluginDependency dependency, string version) =>
        dependency.VersionRange is not null
        && VersionRange.TryParse(dependency.VersionRange, out var range)
        && NuGetVersion.TryParse(version, out var available)
        && range.Satisfies(available);
}

/// <summary>
/// Adds NuGet package folders as a plugin source to <c>builder.Plugins</c>.
/// </summary>
public static class PluginPackageExtensions
{
    extension(PluginCollection plugins)
    {
        /// <summary>
        /// Adds all plugin packages from a folder of NuGet packages (.nupkg files).
        /// </summary>
        /// <remarks>
        /// <para>
        /// A package is a plugin if it has the package type <c>PlugwrightPlugin</c>. It must contain an assembly named like
        /// the package ID for net10.0. All other packages in the folder are library packages: they are only loaded when a
        /// plugin package depends on them, in the lowest version that matches. Dependencies that the host already provides,
        /// such as Microsoft.Extensions.*, need no package.
        /// </para>
        /// <para>
        /// Packages are read into memory and loaded from there; nothing is extracted, except native libraries, which go to
        /// a temporary folder of the process. Each plugin package gets its own load context. A relative path is resolved
        /// against the host's content root. If the folder does not exist, a warning is logged.
        /// </para>
        /// </remarks>
        /// <param name="path">The package folder, absolute or relative to the content root.</param>
        /// <returns>The collection, for chaining.</returns>
        /// <exception cref="InvalidOperationException"><c>builder.ApplyPlugins()</c> has already been called.</exception>
        /// <exception cref="ArgumentException">The path is empty.</exception>
        /// <example>
        /// <code language="csharp">
        /// builder.Plugins.AddFromPackages("packages");
        /// </code>
        /// </example>
        public PluginCollection AddFromPackages(string path)
        {
            ArgumentNullException.ThrowIfNull(plugins);
            ArgumentException.ThrowIfNullOrWhiteSpace(path);
            return plugins.AddSource(new PluginPackageSource(path));
        }
    }
}
