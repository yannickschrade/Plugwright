using System.Reflection;

namespace Plugwright;

/// <summary>
/// An assembly of the application, or one it references, added with <see cref="PluginCollection.AddFromAssembly"/>.
/// </summary>
public sealed class PluginAssemblySource : PluginSource
{
    internal PluginAssemblySource(Assembly assembly)
    {
        Assembly = assembly;
    }

    /// <summary>
    /// Gets the assembly whose plugin types are added.
    /// </summary>
    public Assembly Assembly { get; }

    /// <summary>
    /// Returns a description of the source, for example <c>Assembly Acme.Orders</c>.
    /// </summary>
    /// <returns>A description of the source.</returns>
    public override string ToString() => $"Assembly {Assembly.GetName().Name}";
}
