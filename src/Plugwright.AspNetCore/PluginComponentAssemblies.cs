using System.Reflection;

namespace Plugwright.AspNetCore;

/// <summary>
/// Collects the component assemblies from the active plugins of the <see cref="PluginReport"/>.
/// </summary>
internal sealed class PluginComponentAssemblies(PluginReport report) : IPluginComponentAssemblies
{
    public IReadOnlyList<Assembly> Assemblies { get; } = [.. report.Plugins
        .Where(plugin => plugin.Instance is IBlazorPlugin)
        .Select(plugin => plugin.PluginType.Assembly)
        .Distinct()];
}
