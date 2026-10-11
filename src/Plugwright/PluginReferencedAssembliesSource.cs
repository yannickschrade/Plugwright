namespace Plugwright;

/// <summary>
/// The assemblies the application references whose names start with a prefix, added with
/// <see cref="PluginCollection.AddFromReferencedAssemblies"/>.
/// </summary>
public sealed class PluginReferencedAssembliesSource : PluginSource
{
    internal PluginReferencedAssembliesSource(string prefix)
    {
        Prefix = prefix;
    }

    /// <summary>
    /// Gets the start of the assembly names, as it was passed to <see cref="PluginCollection.AddFromReferencedAssemblies"/>.
    /// </summary>
    public string Prefix { get; }

    /// <summary>
    /// Returns a description of the source, for example <c>Referenced assemblies Acme.Plugins</c>.
    /// </summary>
    /// <returns>A description of the source.</returns>
    public override string ToString() => $"Referenced assemblies {Prefix}";
}
