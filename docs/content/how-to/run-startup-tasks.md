# Run startup tasks

Some plugins must prepare something before the application serves its first request: register a database schema, migrate data, fill a cache. A startup task does that asynchronously, after the host is built and before anything else starts.

The example comes from `samples/SplitHosts` in the repository: the backend plugin loads its orders before the API answers.

## Write a startup task

A startup task implements [`IPluginStartup`](../api/Plugwright.IPluginStartup.md). It is created from the service container, so it receives services through its constructor:

```csharp
--8<-- "samples/SplitHosts/Acme.Orders/OrdersPlugin.cs:startup"
```

## Add it in the plugin

Call [`AddStartup<T>()`](../api/Plugwright.PluginContext.md#Plugwright_PluginContext_AddStartup__1) in `Configure`:

```csharp
--8<-- "samples/SplitHosts/Acme.Orders/OrdersPlugin.cs:plugin"
```

When the host starts, the log shows how long the task took:

```text
info: Plugwright[1009]
      Startup task Acme.Orders.LoadOrdersStartup of plugin Acme.Orders completed in 102 ms
```

## When and how startup tasks run

- **After `Build()`, before everything else.** Startup tasks run when the host starts, before the `StartAsync` of any hosted service, the web server included. A web application does not accept requests before all startup tasks are done.
- **One after the other.** Tasks never run in parallel: in the dependency order of their plugins, and within a plugin in the order they were added. A plugin can rely on the startup tasks of its dependencies having finished.
- **In their own scope.** Each task gets its own service scope, so it can use scoped services such as a database context.
- **Cancelable.** The token passed to `StartAsync` is the host's: it is canceled when the application stops before it has started.

## When a startup task fails

| Plugin | Result |
| --- | --- |
| [Required](required-and-optional-plugins.md) | The host does not start. `StartAsync` or `Run` throws a [`PluginException`](../api/Plugwright.PluginException.md). |
| Failure mode `FailFast` (the default) | The host does not start. |
| Failure mode `SkipAndLog` | The plugin becomes `Failed` in the [catalog](inspect-plugins-at-runtime.md). The startup tasks of plugins that depend on it are skipped, and those plugins become `DependencyUnavailable`. The host starts. |

Skipped plugins keep their services: the service container cannot change after it is built. Where that matters, check the plugin's state with [`IPluginCatalog`](../api/Plugwright.IPluginCatalog.md).

## Startup task or hosted service?

| | Startup task | `IHostedService` / `BackgroundService` |
| --- | --- | --- |
| Runs | once, before the application starts | alongside the application, until it stops |
| Order | dependency order of the plugins, strictly one after the other | registration order; may start concurrently |
| Web server | waits until all tasks are done | may already accept requests |
| Failure | follows the plugin's failure mode, recorded in the catalog | stops the host, or is ignored, depending on `HostOptions` |
| Use for | migrations, schema registration, warming caches the application needs | background work, listeners, timers |

There is no stop hook. Clean up in a hosted service or with `IAsyncDisposable`.

## See also

- [Required and optional plugins](required-and-optional-plugins.md)
- [Log events and telemetry](../reference/log-events.md): events 1009 to 1011 and the activity `Plugwright.Startup`
