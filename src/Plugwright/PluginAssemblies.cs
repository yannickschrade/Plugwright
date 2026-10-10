using System.Reflection;
using System.Runtime.Loader;

namespace Plugwright;

/// <summary>
/// Where the assemblies of one plugin come from: a plugin folder, or a package. Used by <see cref="PluginLoadContext"/>.
/// </summary>
internal abstract class PluginAssemblies
{
    /// <summary>
    /// Gets the name of the plugin's main assembly, also the name of its load context.
    /// </summary>
    public abstract string MainAssemblyName { get; }

    /// <summary>
    /// Loads the plugin's main assembly into its load context.
    /// </summary>
    public abstract Assembly LoadMainAssembly(AssemblyLoadContext context);

    /// <summary>
    /// Loads a private dependency of the plugin, or returns <see langword="null"/> if the plugin does not ship it.
    /// </summary>
    public abstract Assembly? LoadPrivateAssembly(AssemblyLoadContext context, AssemblyName assemblyName);

    /// <summary>
    /// Returns the path of a native library of the plugin, checked and ready to load,
    /// or <see langword="null"/> if the plugin does not ship it.
    /// </summary>
    public abstract string? ResolveUnmanagedDll(string unmanagedDllName);
}
