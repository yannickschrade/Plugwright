using System.Reflection;
using System.Runtime.Loader;

namespace Plugwright;

/// <summary>
/// The load context of one plugin folder. It resolves the plugin's own dependencies from the folder
/// and takes shared assemblies from the default load context, so that the plugin and the host use the same types.
/// </summary>
/// <remarks>
/// Assemblies of plugins this plugin depends on come from those plugins' load contexts, so both use the same types.
/// Shared are the plugin contract (Plugwright) and Microsoft.Extensions.*. They come from the host whenever the host
/// has them, regardless of the version the plugin ships. Everything else, including framework assemblies the plugin
/// does not ship, follows the usual rules: the plugin's own copy first, then the default context.
/// </remarks>
internal sealed class PluginLoadContext : AssemblyLoadContext
{
    private readonly AssemblyDependencyResolver _resolver;
    private readonly IReadOnlyDictionary<string, Assembly> _dependencies;

    /// <param name="mainAssemblyPath">The plugin's main assembly.</param>
    /// <param name="dependencies">The main assemblies of the plugins this plugin depends on, by assembly name.</param>
    public PluginLoadContext(string mainAssemblyPath, IReadOnlyDictionary<string, Assembly> dependencies)
        : base(name: Path.GetFileNameWithoutExtension(mainAssemblyPath), isCollectible: false)
    {
        _resolver = new AssemblyDependencyResolver(mainAssemblyPath);
        _dependencies = dependencies;
    }

    public static bool IsShared(AssemblyName assemblyName)
    {
        var name = assemblyName.Name;
        return name is "Plugwright" or "Plugwright.Abstractions"
            || (name is not null && name.StartsWith("Microsoft.Extensions.", StringComparison.Ordinal));
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        if (assemblyName.Name is not null && _dependencies.TryGetValue(assemblyName.Name, out var dependency))
        {
            return dependency;
        }

        if (IsShared(assemblyName) && TryLoadFromDefault(assemblyName) is { } shared)
        {
            return shared;
        }

        var path = _resolver.ResolveAssemblyToPath(assemblyName);
        return path is null ? null : LoadFromAssemblyPath(path);
    }

    protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
    {
        var path = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
        return path is null ? IntPtr.Zero : LoadUnmanagedDllFromPath(path);
    }

    private static Assembly? TryLoadFromDefault(AssemblyName assemblyName)
    {
        try
        {
            // Without a version, the default context returns the host's copy, whatever its version.
            return Default.LoadFromAssemblyName(new AssemblyName(assemblyName.Name!));
        }
        catch (FileNotFoundException)
        {
            return null;
        }
    }
}
