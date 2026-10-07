using NuGet.Packaging.Core;
using NuGet.Versioning;

namespace Plugwright;

/// <summary>
/// A folder of NuGet packages added with <see cref="PluginPackageExtensions.AddFromPackages(PluginCollection, string, Action{PluginPackageOptions})"/>.
/// </summary>
public sealed class PluginPackageSource : PluginSource, IDiscoveringPluginSource
{
    internal PluginPackageSource(string path, PluginPackageOptions options)
    {
        Path = path;
        Options = options;
    }

    /// <summary>
    /// Gets the path as it was passed to <see cref="PluginPackageExtensions.AddFromPackages(PluginCollection, string, Action{PluginPackageOptions})"/>.
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

        // A package is verified under its file name without .nupkg, so that versions of a library do not collide.
        if (verify
            && context.VerificationEnabled
            && !context.Verify(this, System.IO.Path.GetFileNameWithoutExtension(path), path, [PluginDiscoveryContext.CreateFile(fileName, content)]))
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

        var problem = resolver.Resolve(package, assemblies, nativeLibraries, pluginDependencies, visited: [package.Id]);
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

    private static bool SatisfiesVersionRange(PluginDependency dependency, string version) =>
        dependency.VersionRange is not null
        && VersionRange.TryParse(dependency.VersionRange, out var range)
        && NuGetVersion.TryParse(version, out var available)
        && range.Satisfies(available);
}

/// <summary>
/// Resolves the dependencies of plugin packages: plugin packages become plugin dependencies; library packages come from
/// the package folder or the download folder in the lowest version that matches, or are downloaded from the feed;
/// dependencies the host already provides need no package.
/// </summary>
internal sealed class PackageDependencyResolver
{
    private static readonly Lazy<HashSet<string>> s_hostAssemblies = new(() =>
        ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Select(Path.GetFileNameWithoutExtension)
            .ToHashSet(StringComparer.OrdinalIgnoreCase)!);

    private readonly PluginPackageSource _source;
    private readonly PluginDiscoveryContext _context;
    private readonly HashSet<string> _pluginIds;
    private readonly Dictionary<string, List<PluginPackage>> _libraries;
    private readonly PackageFeed? _feed;
    private readonly string _downloadDirectory;

    public PackageDependencyResolver(
        PluginPackageSource source,
        PluginDiscoveryContext context,
        IEnumerable<PluginPackage> plugins,
        IEnumerable<PluginPackage> libraries,
        PackageFeed? feed,
        string downloadDirectory)
    {
        _source = source;
        _context = context;
        _pluginIds = plugins.Select(package => package.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        _libraries = libraries
            .GroupBy(package => package.Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.OrdinalIgnoreCase);
        _feed = feed;
        _downloadDirectory = downloadDirectory;
    }

    /// <summary>
    /// Adds the assemblies of all library packages a package needs, transitively, and collects its plugin dependencies.
    /// </summary>
    /// <returns>A description of the first problem, or <see langword="null"/>.</returns>
    public string? Resolve(
        PluginPackage package,
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

            if (_pluginIds.Contains(dependency.Id))
            {
                pluginDependencies.Add(new PluginDependency(dependency.Id, dependency.VersionRange.MinVersion?.ToNormalizedString())
                {
                    VersionRange = dependency.VersionRange.ToNormalizedString(),
                });
                continue;
            }

            var library = FindLibrary(dependency);
            if (library is null && !s_hostAssemblies.Value.Contains(dependency.Id) && _feed is not null)
            {
                if (Download(dependency) is { } downloadProblem)
                {
                    return $"Package '{package}' depends on package '{dependency.Id}' {dependency.VersionRange.ToNormalizedString()}, "
                        + $"which is not in the package folder. {downloadProblem}";
                }

                library = FindLibrary(dependency);
            }

            if (library is not null)
            {
                foreach (var (name, content) in library.Assemblies)
                {
                    assemblies.TryAdd(name, content);
                }

                foreach (var (name, content) in library.NativeLibraries)
                {
                    nativeLibraries.TryAdd(name, content);
                }

                if (Resolve(library, assemblies, nativeLibraries, pluginDependencies, visited) is { } problem)
                {
                    return problem;
                }

                continue;
            }

            if (s_hostAssemblies.Value.Contains(dependency.Id))
            {
                continue;
            }

            var available = _libraries.GetValueOrDefault(dependency.Id, []).Select(other => other.Version.ToNormalizedString()).ToList();
            return available.Count == 0
                ? $"Package '{package}' depends on package '{dependency.Id}' {dependency.VersionRange.ToNormalizedString()}, "
                    + "which is neither in the package folder nor provided by the host."
                : $"Package '{package}' depends on package '{dependency.Id}' {dependency.VersionRange.ToNormalizedString()}, "
                    + $"but the package folder only has version {string.Join(", ", available)}.";
        }

        return null;
    }

    /// <summary>
    /// Downloads the lowest matching version of a library package, verifies it, and stores it in the download folder,
    /// so that the next start finds it without network.
    /// </summary>
    /// <returns>A description of the problem, or <see langword="null"/> if the package is now available.</returns>
    private string? Download(PackageDependency dependency)
    {
        NuGetVersion version;
        byte[] content;
        try
        {
            (version, content) = _feed!.Download(dependency.Id, dependency.VersionRange);
        }
        catch (InvalidOperationException exception)
        {
            return exception.Message;
        }

        var path = Path.Combine(_downloadDirectory, $"{dependency.Id}.{version.ToNormalizedString()}.nupkg");
        if (_source.ReadPackage(path, content, _context, _source.Options.VerifyDownloads) is not { } package)
        {
            return $"The downloaded package '{Path.GetFileName(path)}' cannot be used.";
        }

        if (package.IsPlugin)
        {
            return $"The feed provides it as a plugin package. Plugin packages are not downloaded; install it in the package folder.";
        }

        try
        {
            Directory.CreateDirectory(_downloadDirectory);
            File.WriteAllBytes(path, content);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return $"The downloaded package could not be stored in '{_downloadDirectory}': {exception.Message}";
        }

        var downloadedVersion = package.Version.ToNormalizedString();
        PluginLog.PackageDownloaded(_context.Logger, package.Id, downloadedVersion, _feed.Name, path);

        if (!_libraries.TryGetValue(package.Id, out var versions))
        {
            _libraries[package.Id] = versions = [];
        }

        versions.Add(package);
        return null;
    }

    private PluginPackage? FindLibrary(PackageDependency dependency) =>
        _libraries.GetValueOrDefault(dependency.Id, [])
            .Where(library => dependency.VersionRange.Satisfies(library.Version))
            .MinBy(library => library.Version);
}
