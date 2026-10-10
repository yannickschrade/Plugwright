namespace Plugwright;

/// <summary>
/// A plugin source that finds its plugins itself, such as the package source of Plugwright.NuGet.
/// </summary>
internal interface IDiscoveringPluginSource
{
    /// <summary>
    /// Finds, verifies and reads the plugins of the source, without loading them for execution.
    /// </summary>
    IEnumerable<PluginUnit> Discover(PluginDiscoveryContext context);
}
