using System.Reflection;
using System.Runtime.Loader;

namespace Plugwright;

/// <summary>
/// The assemblies of a plugin folder, resolved through the plugin's <c>.deps.json</c>. When the plugin was verified,
/// assemblies are loaded from the verified bytes and native libraries only if they still match them.
/// </summary>
internal sealed class FolderAssemblies : PluginAssemblies
{
    private readonly string _mainAssemblyPath;
    private readonly AssemblyDependencyResolver _resolver;
    private readonly VerifiedFiles? _verifiedFiles;

    public FolderAssemblies(string mainAssemblyPath, VerifiedFiles? verifiedFiles)
    {
        _mainAssemblyPath = mainAssemblyPath;
        _resolver = new AssemblyDependencyResolver(mainAssemblyPath);
        _verifiedFiles = verifiedFiles;
    }

    public override string MainAssemblyName => Path.GetFileNameWithoutExtension(_mainAssemblyPath);

    public override Assembly LoadMainAssembly(AssemblyLoadContext context) => Load(context, _mainAssemblyPath);

    public override Assembly? LoadPrivateAssembly(AssemblyLoadContext context, AssemblyName assemblyName)
    {
        var path = _resolver.ResolveAssemblyToPath(assemblyName);
        return path is null ? null : Load(context, path);
    }

    public override string? ResolveUnmanagedDll(string unmanagedDllName)
    {
        var path = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);

        // Native libraries can only be loaded from disk. Check right before loading that the file is still the verified one.
        if (path is not null)
        {
            _verifiedFiles?.ThrowIfChanged(path);
        }

        return path;
    }

    private Assembly Load(AssemblyLoadContext context, string path)
    {
        if (_verifiedFiles is null)
        {
            return context.LoadFromAssemblyPath(path);
        }

        using var stream = new MemoryStream(_verifiedFiles.GetContent(path), writable: false);
        return context.LoadFromStream(stream);
    }
}
