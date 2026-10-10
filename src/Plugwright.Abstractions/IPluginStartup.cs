namespace Plugwright;

/// <summary>
/// An asynchronous task that prepares a plugin before the application starts, for example registering a database schema
/// or migrating data. Register it in <see cref="IPlugin.Configure"/> with <see cref="PluginContext.AddStartup{TStartup}"/>.
/// </summary>
/// <remarks>
/// <para>
/// Startup tasks run once per start, after the host is built and before any hosted service starts, including the web
/// server: an application does not answer requests before all startup tasks are done. They run one after the other, in
/// the dependency order of their plugins and, within a plugin, in the order they were added.
/// </para>
/// <para>
/// Each task is created from the service container in its own scope, so it can receive services, also scoped ones such
/// as a database context, through its constructor.
/// </para>
/// <para>
/// If a task fails, a required plugin stops the application. Otherwise the plugin's failure mode decides: with
/// <c>FailFast</c> the application does not start; with <c>SkipAndLog</c> the plugin becomes failed in the
/// <see cref="IPluginCatalog"/> and the startup tasks of plugins that depend on it are skipped. Their services stay
/// registered, because the service container cannot change after it is built; check <see cref="IPluginCatalog"/> where
/// that matters. There is no stop hook: clean up in a hosted service or with <see cref="IAsyncDisposable"/>.
/// </para>
/// <para>
/// Use a startup task for work that must be finished before the application runs. Work that runs alongside the
/// application belongs in an <c>IHostedService</c> or <c>BackgroundService</c>.
/// </para>
/// </remarks>
/// <example>
/// <code language="csharp">
/// public sealed class MigrateOrdersStartup(OrdersDbContext db) : IPluginStartup
/// {
///     public Task StartAsync(CancellationToken cancellationToken) =&gt; db.Database.MigrateAsync(cancellationToken);
/// }
///
/// public sealed class OrdersPlugin : IPlugin
/// {
///     public void Configure(PluginContext context) =&gt; context.AddStartup&lt;MigrateOrdersStartup&gt;();
/// }
/// </code>
/// </example>
public interface IPluginStartup
{
    /// <summary>
    /// Runs the task.
    /// </summary>
    /// <param name="cancellationToken">Signals that the application is stopping before it has started.</param>
    /// <returns>A task that completes when the startup task is done.</returns>
    Task StartAsync(CancellationToken cancellationToken);
}
