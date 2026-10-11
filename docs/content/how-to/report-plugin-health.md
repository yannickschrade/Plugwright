# Report plugin health

Health checks tell load balancers, orchestrators and dashboards whether an application works. With plugins, two questions need an answer: did all plugins start, and do the running plugins work? Plugwright answers the first, each plugin answers the second. The examples come from `samples/SplitHosts`, a web application, and `samples/WorkerHealth`, a worker without web server, in the repository.

## Report plugins that did not start

[`AddPlugins()`](../api/Plugwright.PluginHealthChecksBuilderExtensions.md) adds a health check for the plugin system:

```csharp
--8<-- "samples/SplitHosts/Api/Program.cs:host"
```

The check is `Healthy` while every plugin that is turned on runs. It reports `Degraded` for plugins that do not run because of an error:

| Plugin state | Health |
| --- | --- |
| `Loaded` | Healthy |
| `Disabled` | Healthy: turning a plugin off is a decision, not an error. |
| `DependencyUnavailable`, because a plugin it depends on is turned off | Healthy |
| `DependencyUnavailable`, because a dependency is missing, has the wrong version or failed | Degraded |
| `Failed`, also after a failed [startup task](run-startup-tasks.md) | Degraded |
| `VerificationFailed` | Degraded |
| `NoHandler` | Degraded |

The result's description names the plugins, and its data holds the state of each one. See [Plugin states](../explanation/plugin-states.md) for what the states mean.

Plugins can only fail without stopping the application if they are skipped, with the [failure mode](handle-plugin-failures.md) `SkipAndLog`. Required plugins need no health check: a problem with one stops the application.

`AddPlugins()` takes a name, the status to report on errors and tags, like other health checks:

```csharp
builder.Services.AddHealthChecks().AddPlugins("plugins", HealthStatus.Unhealthy, ["ready"]);
```

## Report health without a web server

Health checks are not tied to ASP.NET Core. `AddHealthChecks()` and `AddPlugins()` work in every application built on the .NET Generic Host, including console and worker applications. Only `MapHealthChecks()`, which answers health requests over HTTP, needs ASP.NET Core.

Without a web server, either ask `HealthCheckService` yourself with `CheckHealthAsync()`, or let a publisher receive the results: `AddHealthChecks()` registers a hosted service that runs all checks periodically and hands the report to every registered `IHealthCheckPublisher`. The worker in `samples/WorkerHealth` writes the status to a file, which a Kubernetes exec probe or a monitoring agent can read:

```csharp
--8<-- "samples/WorkerHealth/FileHealthPublisher.cs:publisher"
```

```csharp
--8<-- "samples/WorkerHealth/Program.cs:host"
```

The sample's plugin fails without a connection string, so the file says:

```text
Degraded
plugins: Degraded 1 plugin(s) do not run because of an error: WorkerHealth (Failed)
```

`HealthCheckPublisherOptions` sets when the checks run: `Delay` after the start (default 5 seconds) and then every `Period` (default 30 seconds).

## Let plugins report their own health

Whether a running plugin works, for example whether its database is reachable, only the plugin knows. Register a health check in [`IPlugin.Configure`](../api/Plugwright.IPlugin.md#Plugwright_IPlugin_Configure_Plugwright_PluginContext_), like any other service:

```csharp
--8<-- "samples/SplitHosts/Acme.Orders/OrdersPlugin.cs:plugin"
```

A plugin that is turned off never runs `Configure`, so its health check does not exist and does not report anything. If `Configure` throws and the plugin is skipped, the services it registered, its health check included, are removed again.

## See also

- [Inspect plugins at runtime](inspect-plugins-at-runtime.md)
- [Health checks in ASP.NET Core](https://learn.microsoft.com/aspnet/core/host-and-deploy/health-checks)
