using System.Reflection;
using System.Runtime.Loader;
using System.Security.Cryptography;

namespace Plugwright;

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

/// <summary>
/// A temporary folder for the native libraries of plugin packages, one per process, deleted when the process exits.
/// </summary>
internal static class NativeLibraryFolder
{
    private static readonly Lazy<string> s_folder = new(CreateFolder);

    /// <summary>
    /// Writes a native library and checks right before returning that the file on disk is exactly the content.
    /// </summary>
    /// <returns>The path to load the library from.</returns>
    public static string Write(string pluginId, string fileName, byte[] content)
    {
        var folder = Directory.CreateDirectory(Path.Combine(s_folder.Value, pluginId)).FullName;
        var path = Path.Combine(folder, fileName);
        if (!File.Exists(path))
        {
            File.WriteAllBytes(path, content);
        }

        if (!CryptographicOperations.FixedTimeEquals(SHA256.HashData(content), SHA256.HashData(File.ReadAllBytes(path))))
        {
            throw new FileLoadException($"The native library '{path}' was changed after it was written and is not loaded.");
        }

        return path;
    }

    private static string CreateFolder()
    {
        var folder = Path.Combine(Path.GetTempPath(), "plugwright-native", $"{Environment.ProcessId}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(folder);
        AppDomain.CurrentDomain.ProcessExit += (_, _) =>
        {
            try
            {
                Directory.Delete(folder, recursive: true);
            }
            catch (IOException)
            {
                // Loaded libraries may still be locked (Windows); the folder is left behind.
            }
            catch (UnauthorizedAccessException)
            {
            }
        };
        return folder;
    }
}
