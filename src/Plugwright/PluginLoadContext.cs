using System.Reflection;
using System.Runtime.Loader;

namespace Plugwright;

/// <summary>
/// The load context of one plugin. It loads the plugin's own assemblies through <see cref="PluginAssemblies"/>
/// and takes shared assemblies from the default load context, so that the plugin and the host use the same types.
/// </summary>
/// <remarks>
/// Assemblies of plugins this plugin depends on come from those plugins' load contexts, so both use the same types.
/// Shared assemblies (<see cref="SharedAssemblies"/>) come from the host whenever the host has them, regardless of the
/// version the plugin ships. Everything else, including framework assemblies the plugin does not ship, follows the usual
/// rules: the plugin's own copy first, then the default context.
/// </remarks>
internal sealed class PluginLoadContext : AssemblyLoadContext
{
    private readonly PluginAssemblies _assemblies;
    private readonly IReadOnlyDictionary<string, Assembly> _dependencies;
    private readonly SharedAssemblies _sharedAssemblies;

    /// <param name="assemblies">The plugin's own assemblies.</param>
    /// <param name="dependencies">The main assemblies of the plugins this plugin depends on, by assembly name.</param>
    /// <param name="sharedAssemblies">The assemblies that come from the host.</param>
    public PluginLoadContext(
        PluginAssemblies assemblies, IReadOnlyDictionary<string, Assembly> dependencies, SharedAssemblies sharedAssemblies)
        : base(name: assemblies.MainAssemblyName, isCollectible: false)
    {
        _assemblies = assemblies;
        _dependencies = dependencies;
        _sharedAssemblies = sharedAssemblies;
    }

    public Assembly LoadMainAssembly() => _assemblies.LoadMainAssembly(this);

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        if (assemblyName.Name is not null && _dependencies.TryGetValue(assemblyName.Name, out var dependency))
        {
            return dependency;
        }

        if (_sharedAssemblies.Contains(assemblyName) && TryLoadFromDefault(assemblyName) is { } shared)
        {
            return shared;
        }

        return _assemblies.LoadPrivateAssembly(this, assemblyName);
    }

    protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
    {
        var path = _assemblies.ResolveUnmanagedDll(unmanagedDllName);
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
