namespace Plugwright.AspNetCore;

/// <summary>
/// Where <c>app.MapPluginStaticAssets()</c> serves the static web assets of plugins from. Set with
/// <see cref="PluginStaticAssetsOptions.Source"/>.
/// </summary>
public enum PluginStaticAssetSource
{
    /// <summary>
    /// From disk: plugin folders from their <c>wwwroot</c> folder, packages from a temporary folder of the process that
    /// their assets are written to at startup. Uses no extra memory. Files on disk can be changed after the plugin was
    /// verified, and changed files are served.
    /// </summary>
    Disk,

    /// <summary>
    /// Like the plugin's assemblies: packages from memory; plugin folders from the verified bytes in memory if verifiers
    /// are registered, otherwise from disk. Serves exactly what was verified, at the cost of memory for the assets of
    /// packages.
    /// </summary>
    Memory,
}
