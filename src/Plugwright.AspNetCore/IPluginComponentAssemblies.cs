using System.Reflection;

namespace Plugwright;

/// <summary>
/// The assemblies of the active Blazor plugins, which contain their Razor components. Registered in the service container
/// by <see cref="BlazorPluginHandler"/>.
/// </summary>
/// <remarks>
/// Pass them to the router, so that it finds the plugins' pages, and to <c>MapRazorComponents</c> with
/// <c>AddPluginAssemblies(app)</c>.
/// </remarks>
/// <example>
/// <code language="razor">
/// @inject IPluginComponentAssemblies PluginAssemblies
///
/// &lt;Router AppAssembly="typeof(Program).Assembly" AdditionalAssemblies="PluginAssemblies.Assemblies"&gt;
///     ...
/// &lt;/Router&gt;
/// </code>
/// </example>
public interface IPluginComponentAssemblies
{
    /// <summary>
    /// Gets the assemblies of all active plugins that implement <see cref="IBlazorPlugin"/>, in dependency order and
    /// without duplicates.
    /// </summary>
    IReadOnlyList<Assembly> Assemblies { get; }
}

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
