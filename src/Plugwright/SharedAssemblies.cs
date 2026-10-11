using System.Reflection;

namespace Plugwright;

/// <summary>
/// The assemblies that plugins always take from the host, so that their types are the same in the host and in every
/// plugin: the plugin contracts, <c>Microsoft.Extensions.*</c>, <c>Microsoft.AspNetCore.*</c> and the assemblies the
/// host shares with <see cref="PluginCollection.ShareAssembly"/> and <see cref="PluginCollection.ShareAssemblies"/>.
/// </summary>
internal sealed class SharedAssemblies
{
    private static readonly string[] s_builtInNames =
        ["Plugwright", "Plugwright.Abstractions", "Plugwright.AspNetCore", "Plugwright.AspNetCore.Abstractions"];

    private static readonly string[] s_builtInPrefixes = ["Microsoft.Extensions", "Microsoft.AspNetCore"];

    private readonly HashSet<string> _names = new(s_builtInNames, StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _prefixes = [.. s_builtInPrefixes];

    /// <summary>
    /// Shares one assembly by its name.
    /// </summary>
    public void AddName(string name) => _names.Add(name);

    /// <summary>
    /// Shares the assembly with this name and all assemblies whose names continue it after a dot.
    /// </summary>
    public void AddPrefix(string prefix) => _prefixes.Add(prefix);

    /// <summary>
    /// Returns whether plugins take this assembly from the host.
    /// </summary>
    public bool Contains(AssemblyName assemblyName) =>
        assemblyName.Name is { } name && (_names.Contains(name) || _prefixes.Any(prefix => AssemblyNamePrefix.Matches(name, prefix)));
}
