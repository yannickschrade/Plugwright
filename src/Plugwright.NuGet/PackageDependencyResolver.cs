using NuGet.Packaging.Core;
using NuGet.Versioning;

namespace Plugwright.NuGet;

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
                foreach ((var name, var content) in library.Assemblies)
                {
                    assemblies.TryAdd(name, content);
                }

                foreach ((var name, var content) in library.NativeLibraries)
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
