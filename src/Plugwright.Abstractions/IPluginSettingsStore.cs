namespace Plugwright;

/// <summary>
/// Saves plugin settings and the on/off state of plugins to the application-managed plugin settings file,
/// <c>pluginsettings.json</c> by default. Available from the service container unless the file is turned off.
/// </summary>
/// <remarks>
/// <para>
/// The file is managed by the application: it is rewritten on every save, so comments and formatting are lost.
/// Saved settings take effect while the application runs for plugins that use <c>IOptionsMonitor</c>; turning a
/// plugin on or off takes effect at the next start.
/// </para>
/// <para>
/// Values in the file win over all other configuration sources, including environment variables.
/// Required plugins cannot be turned off: the next start fails with an exception that names them.
/// </para>
/// </remarks>
/// <example>
/// <code language="csharp">
/// app.MapPost("/admin/plugins/{id}/disable", async (string id, IPluginSettingsStore store) =&gt;
/// {
///     await store.SetEnabledAsync(id, false);
///     return Results.Ok("Takes effect after a restart.");
/// });
/// </code>
/// </example>
public interface IPluginSettingsStore
{
    /// <summary>
    /// Gets the full path of the plugin settings file.
    /// </summary>
    string FilePath { get; }

    /// <summary>
    /// Saves whether a plugin is loaded at the next start, as <c>Plugwright:Plugins:{pluginId}:Enabled</c>.
    /// </summary>
    /// <param name="pluginId">The plugin ID.</param>
    /// <param name="enabled"><see langword="false"/> to turn the plugin off.</param>
    /// <param name="cancellationToken">Cancels the save.</param>
    Task SetEnabledAsync(string pluginId, bool enabled, CancellationToken cancellationToken = default);

    /// <summary>
    /// Replaces a plugin's settings, <c>Plugwright:Plugins:{pluginId}:Settings</c>, with the JSON form of
    /// <paramref name="settings"/>.
    /// </summary>
    /// <typeparam name="TSettings">The type of the settings.</typeparam>
    /// <param name="pluginId">The plugin ID.</param>
    /// <param name="settings">The new settings.</param>
    /// <param name="cancellationToken">Cancels the save.</param>
    Task SaveSettingsAsync<TSettings>(string pluginId, TSettings settings, CancellationToken cancellationToken = default);
}
