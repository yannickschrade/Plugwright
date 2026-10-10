namespace Plugwright;

/// <summary>
/// All plugins the plugin system knows about, with their metadata and state, also those that were not loaded.
/// Available from the service container after <c>builder.ApplyPlugins()</c>.
/// </summary>
/// <remarks>
/// The catalog is safe to use from several threads. States can still change after startup: a plugin whose startup
/// task fails becomes <see cref="PluginState.Failed"/>. <see cref="Plugins"/> returns a snapshot.
/// </remarks>
/// <example>
/// <code language="csharp">
/// public sealed class ReportsService(IPluginCatalog catalog)
/// {
///     public bool CanExportToPdf =&gt; catalog.Find("Acme.Pdf")?.State == PluginState.Loaded;
/// }
/// </code>
/// </example>
public interface IPluginCatalog
{
    /// <summary>
    /// Gets all known plugins.
    /// </summary>
    IReadOnlyList<PluginInfo> Plugins { get; }

    /// <summary>
    /// Returns the plugin with the given ID, compared without case, or <see langword="null"/> if there is none.
    /// </summary>
    /// <param name="pluginId">The plugin ID.</param>
    /// <returns>The plugin, or <see langword="null"/>.</returns>
    PluginInfo? Find(string pluginId);
}
