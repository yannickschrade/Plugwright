using System.Reflection;
using System.Runtime.Loader;

namespace Plugwright.NuGet;

/// <summary>
/// The assemblies of a plugin package and of the library packages it depends on, loaded from memory.
/// Native libraries are written to a temporary folder of this process, because they can only be loaded from disk.
/// </summary>
internal sealed class PackageAssemblies : PluginAssemblies
{
    private readonly string _pluginId;
    private readonly IReadOnlyDictionary<string, byte[]> _assemblies;
    private readonly IReadOnlyDictionary<string, byte[]> _nativeLibraries;

    /// <param name="pluginId">The plugin ID, also the name of the main assembly.</param>
    /// <param name="assemblies">The assemblies of the plugin and its library packages, by assembly name.</param>
    /// <param name="nativeLibraries">The native libraries of the plugin and its library packages, by file name.</param>
    public PackageAssemblies(string pluginId, IReadOnlyDictionary<string, byte[]> assemblies, IReadOnlyDictionary<string, byte[]> nativeLibraries)
    {
        _pluginId = pluginId;
        _assemblies = assemblies;
        _nativeLibraries = nativeLibraries;
    }

    public override string MainAssemblyName => _pluginId;

    public override Assembly LoadMainAssembly(AssemblyLoadContext context) => Load(context, _assemblies[_pluginId]);

    public override Assembly? LoadPrivateAssembly(AssemblyLoadContext context, AssemblyName assemblyName) =>
        assemblyName.Name is not null && _assemblies.TryGetValue(assemblyName.Name, out var content) ? Load(context, content) : null;

    public override string? ResolveUnmanagedDll(string unmanagedDllName)
    {
        var fileName = NativeFileNames(unmanagedDllName).FirstOrDefault(_nativeLibraries.ContainsKey);
        return fileName is null ? null : NativeLibraryFolder.Write(_pluginId, fileName, _nativeLibraries[fileName]);
    }

    private static Assembly Load(AssemblyLoadContext context, byte[] content)
    {
        using var stream = new MemoryStream(content, writable: false);
        return context.LoadFromStream(stream);
    }

    // The runtime asks for "foo"; the package may contain foo.dll, libfoo.so, libfoo.dylib, ...
    private static IEnumerable<string> NativeFileNames(string name)
    {
        yield return name;
        foreach (var extension in new[] { ".dll", ".so", ".dylib" })
        {
            yield return name + extension;
            yield return "lib" + name + extension;
        }
    }
}
