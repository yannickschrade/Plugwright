using System.Runtime.InteropServices;
using NuGet.Frameworks;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Versioning;

namespace Plugwright;

/// <summary>
/// One .nupkg file of a package folder, read into memory: its identity, whether it is a plugin, its dependencies,
/// and the assemblies and native libraries that apply to this application.
/// </summary>
internal sealed class PluginPackage
{
    /// <summary>
    /// The package type that marks a plugin package: <c>&lt;PackageType&gt;PlugwrightPlugin&lt;/PackageType&gt;</c>.
    /// </summary>
    public const string PluginPackageType = "PlugwrightPlugin";

    private static readonly NuGetFramework s_targetFramework = NuGetFramework.Parse("net10.0");

    private PluginPackage(string path, byte[] content, NuspecReader nuspec)
    {
        Path = path;
        Content = content;
        Id = nuspec.GetId();
        Version = nuspec.GetVersion();
        IsPlugin = nuspec.GetPackageTypes().Any(type => type.Name == PluginPackageType);
        Description = NullIfEmpty(nuspec.GetDescription());
        Authors = NullIfEmpty(nuspec.GetAuthors());
    }

    public string Path { get; }

    /// <summary>
    /// Gets the content of the .nupkg file, as read (and verified) once.
    /// </summary>
    public byte[] Content { get; }

    public string Id { get; }

    public NuGetVersion Version { get; }

    public bool IsPlugin { get; }

    public string? Description { get; }

    public string? Authors { get; }

    /// <summary>
    /// Gets the dependencies of the dependency group that applies to net10.0.
    /// </summary>
    public IReadOnlyList<PackageDependency> Dependencies { get; private init; } = [];

    /// <summary>
    /// Gets the assemblies for net10.0 by assembly name. Assemblies for the current runtime replace portable ones.
    /// </summary>
    public IReadOnlyDictionary<string, byte[]> Assemblies { get; private init; } = new Dictionary<string, byte[]>();

    /// <summary>
    /// Gets the native libraries for the current runtime by file name.
    /// </summary>
    public IReadOnlyDictionary<string, byte[]> NativeLibraries { get; private init; } = new Dictionary<string, byte[]>();

    public override string ToString() => $"{Id} {Version.ToNormalizedString()}";

    /// <exception cref="InvalidDataException">The file is not a valid package.</exception>
    public static PluginPackage Read(string path, byte[] content)
    {
        try
        {
            using var reader = new PackageArchiveReader(new MemoryStream(content, writable: false));
            var nuspec = reader.NuspecReader;
            var files = reader.GetFiles().ToList();

            var dependencyGroup = NuGetFrameworkUtility.GetNearest(nuspec.GetDependencyGroups(), s_targetFramework, group => group.TargetFramework);

            return new PluginPackage(path, content, nuspec)
            {
                Dependencies = [.. dependencyGroup?.Packages ?? []],
                Assemblies = ReadAssemblies(reader, files),
                NativeLibraries = ReadNativeLibraries(reader, files),
            };
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            throw new InvalidDataException($"'{System.IO.Path.GetFileName(path)}' is not a valid NuGet package: {exception.Message}", exception);
        }
    }

    private static Dictionary<string, byte[]> ReadAssemblies(PackageArchiveReader reader, List<string> files)
    {
        var assemblies = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        AddAssemblies(reader, files, "lib/", assemblies);

        // Runtime-specific assemblies (runtimes/<rid>/lib/<tfm>/) replace the portable ones, most specific last.
        foreach (var runtime in RuntimeFallbacks().Reverse())
        {
            AddAssemblies(reader, files, $"runtimes/{runtime}/lib/", assemblies);
        }

        return assemblies;
    }

    private static void AddAssemblies(PackageArchiveReader reader, List<string> files, string prefix, Dictionary<string, byte[]> assemblies)
    {
        var byFramework = files
            .Where(file => file.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && file.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            .Select(file => (File: file, Parts: file[prefix.Length..].Split('/')))
            .Where(entry => entry.Parts.Length == 2)
            .GroupBy(entry => NuGetFramework.ParseFolder(entry.Parts[0]))
            .ToList();

        var nearest = new FrameworkReducer().GetNearest(s_targetFramework, byFramework.Select(group => group.Key));
        if (nearest is null)
        {
            return;
        }

        foreach (var (file, _) in byFramework.Single(group => group.Key == nearest))
        {
            assemblies[System.IO.Path.GetFileNameWithoutExtension(file)] = ReadAll(reader, file);
        }
    }

    private static Dictionary<string, byte[]> ReadNativeLibraries(PackageArchiveReader reader, List<string> files)
    {
        var libraries = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var runtime in RuntimeFallbacks().Reverse())
        {
            var prefix = $"runtimes/{runtime}/native/";
            foreach (var file in files.Where(file => file.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && !file.EndsWith('/')))
            {
                libraries[System.IO.Path.GetFileName(file)] = ReadAll(reader, file);
            }
        }

        return libraries;
    }

    /// <summary>
    /// The runtime identifiers to look for, most specific first, for example linux-x64, linux, unix.
    /// </summary>
    private static IEnumerable<string> RuntimeFallbacks()
    {
        var runtime = RuntimeInformation.RuntimeIdentifier;
        yield return runtime;

        var system = runtime.Split('-')[0];
        if (system != runtime)
        {
            yield return system;
        }

        if (!OperatingSystem.IsWindows())
        {
            yield return "unix";
        }
    }

    private static byte[] ReadAll(PackageArchiveReader reader, string file)
    {
        using var stream = reader.GetStream(file);
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return memory.ToArray();
    }

    private static string? NullIfEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
}
