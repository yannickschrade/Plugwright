namespace Plugwright;

/// <summary>
/// Restarts the application, so that changes take effect that need a new start, such as turning plugins on or off.
/// Registered by <c>builder.ApplyPlugins()</c>.
/// </summary>
/// <remarks>
/// <para>
/// How the application restarts is up to the host application, set with <c>builder.Plugins.Options.RestartMode</c>:
/// by default it stops with an exit code, and a service manager such as systemd, Windows services, Docker, Kubernetes
/// or IIS starts it again; alternatively it starts a new process of itself. A host application can also register its
/// own implementation.
/// </para>
/// <para>
/// Plugins and admin pages only ask for the restart; they need not know how the application is run.
/// </para>
/// </remarks>
/// <example>
/// <code language="csharp">
/// app.MapPost("/admin/plugins/{id}/disable", async (string id, IPluginSettingsStore store, IApplicationRestarter restarter) =&gt;
/// {
///     await store.SetEnabledAsync(id, false);
///     restarter.RequestRestart();
/// });
/// </code>
/// </example>
public interface IApplicationRestarter
{
    /// <summary>
    /// Stops the application gracefully so that it starts again. Returns immediately; calling it again has no effect.
    /// </summary>
    void RequestRestart();
}
