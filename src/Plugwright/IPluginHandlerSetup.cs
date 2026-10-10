using Microsoft.Extensions.Hosting;

namespace Plugwright;

/// <summary>
/// Lets a built-in handler of another Plugwright package prepare the builder once, even if no plugin uses it.
/// For example, the Blazor handler always registers the list of component assemblies, so that a host can inject it
/// whether or not a Blazor plugin is enabled.
/// </summary>
internal interface IPluginHandlerSetup
{
    /// <summary>
    /// Called once by <c>builder.ApplyPlugins()</c>, before any plugin is processed.
    /// </summary>
    void Setup(IHostApplicationBuilder builder);
}
