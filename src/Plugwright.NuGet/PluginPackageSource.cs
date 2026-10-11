using NuGet.Versioning;

namespace Plugwright.NuGet;

/// <summary>
/// A folder of NuGet packages added with <see cref="PackagePluginCollectionExtensions.AddFromPackages(PluginCollection, string, Action{PluginPackageOptions})"/>.
/// </summary>
public sealed class PluginPackageSource : PluginSource, IDiscoveringPluginSource
{
    internal PluginPackageSource(string path, PluginPackageOptions options)
    {
        Path = path;
        Options = options;
    }

    /// <summary>
    /// Gets the path as it was passed to <see cref="PackagePluginCollectionExtensions.AddFromPackages(PluginCollection, string, Action{PluginPackageOptions})"/>.
    /// A relative path is resolved against the content root when <c>builder.ApplyPlugins()</c> runs.
    /// </summary>
    public string Path { get; }

    /// <summary>
    /// Gets the options of the package folder, such as the feed for missing packages.
    /// </summary>
    public PluginPackageOptions Options { get; }

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

        var packages = ReadPackages(directory, context, verify: true);
        var downloadDirectory = System.IO.Path.GetFullPath(Options.DownloadDirectory, context.ContentRoot);
        var downloads = Directory.Exists(downloadDirectory)
            ? ReadPackages(downloadDirectory, context, verify: Options.VerifyDownloads).Where(package => !package.IsPlugin)
            : [];

        var resolver = new PackageDependencyResolver(
            this,
            context,
            plugins: packages.Where(package => package.IsPlugin),
            libraries: [.. packages.Where(package => !package.IsPlugin), .. downloads],
            feed: Options.Feed is null ? null : new PackageFeed(Options.Feed, context.ContentRoot, Options.DownloadTimeout),
            downloadDirectory);

        List<PluginUnit> units = [];
        foreach (var package in packages.Where(package => package.IsPlugin))
        {
            if (context.IsDisabled(package.Id))
            {
                units.Add(PluginUnit.Disabled(package.Id, package.Path, "package", new PluginMetadata(package.Id, package.Version.ToNormalizedString())
                {
                    Description = package.Description,
                    Authors = package.Authors,
                }));
                continue;
            }

            if (CreateUnit(package, resolver, context) is { } unit)
            {
                units.Add(unit);
            }
        }

        return units;
    }

    /// <summary>
    /// Reads and verifies a package file, or reports why it cannot be used.
    /// </summary>
    /// <returns>The package, or <see langword="null"/> if it cannot be read or fails verification.</returns>
    /// <param name="path">The path of the package file.</param>
    /// <param name="content">The content of the package file.</param>
    /// <param name="context">The discovery context.</param>
    /// <param name="verify">Whether the package must pass the registered verifiers.</param>
    internal PluginPackage? ReadPackage(string path, byte[] content, PluginDiscoveryContext context, bool verify)
    {
        var fileName = System.IO.Path.GetFileName(path);
        PluginPackage package;
        try
        {
            package = PluginPackage.Read(path, content);
        }
        catch (InvalidDataException exception)
        {
            context.Fail(fileName, path, exception.Message, exception);
            return null;
        }

        // A plugin package that is turned off is neither verified nor loaded; its manifest is all that is read.
        if (package.IsPlugin && context.IsDisabled(package.Id))
        {
            return package;
        }

        // A package is verified under its file name without .nupkg, so that versions of a library do not collide.
        if (verify
            && context.VerificationEnabled
            && !context.Verify(
                this,
                System.IO.Path.GetFileNameWithoutExtension(path),
                path,
                [PluginDiscoveryContext.CreateFile(fileName, content)],
                pluginId: package.IsPlugin ? package.Id : System.IO.Path.GetFileNameWithoutExtension(path)))
        {
            return null;
        }

        return package;
    }

    /// <summary>
    /// Reads every .nupkg file of a folder once into memory and verifies it.
    /// </summary>
    private List<PluginPackage> ReadPackages(string directory, PluginDiscoveryContext context, bool verify)
    {
        List<PluginPackage> packages = [];
        foreach (var path in Directory.EnumerateFiles(directory, "*.nupkg").Order(StringComparer.Ordinal))
        {
            byte[] content;
            try
            {
                content = File.ReadAllBytes(path);
            }
            catch (IOException exception)
            {
                context.Fail(System.IO.Path.GetFileName(path), path, $"The package could not be read: {exception.Message}", exception);
                continue;
            }

            if (ReadPackage(path, content, context, verify) is { } package)
            {
                packages.Add(package);
            }
        }

        return packages;
    }

    private static PluginUnit? CreateUnit(PluginPackage package, PackageDependencyResolver resolver, PluginDiscoveryContext context)
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
        List<PluginStaticWebAssets> staticWebAssets = package.StaticWebAssets is { } own ? [own] : [];

        var problem = resolver.Resolve(package, assemblies, nativeLibraries, pluginDependencies, staticWebAssets, visited: [package.Id]);
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
        var unit = PluginUnit.FromSource(info, package.Path, "package", new PackageAssemblies(package.Id, assemblies, nativeLibraries));
        unit.StaticWebAssets.AddRange(staticWebAssets);
        return unit;
    }

    private static bool SatisfiesVersionRange(PluginDependency dependency, string version) =>
        dependency.VersionRange is not null
        && VersionRange.TryParse(dependency.VersionRange, out var range)
        && NuGetVersion.TryParse(version, out var available)
        && range.Satisfies(available);
}
