# Use Plugwright with .NET Aspire

Plugwright works together with the service defaults of a .NET Aspire solution. Plugins can register their own health checks and OpenTelemetry sources, and the plugin system reports its state through logs and metrics that appear in the Aspire dashboard.

The complete, runnable solution is in `samples/SplitHosts` in the repository: an API host with a backend plugin, a Blazor host with a UI plugin, and an Aspire app host that starts both. Run it with `dotnet run --project samples/SplitHosts/AppHost`.

## Start the hosts from the app host

The app host knows nothing about plugins. It starts the two hosts and gives the web host a reference to the API, so that the UI plugin reaches it through service discovery:

```csharp
--8<-- "samples/SplitHosts/AppHost/AppHost.cs:apphost"
```

## Call AddServiceDefaults before ApplyPlugins

```csharp
--8<-- "samples/SplitHosts/Api/Program.cs:host"
```

Call `builder.AddServiceDefaults()` before `builder.ApplyPlugins()`. Plugins then find health checks, OpenTelemetry and service discovery already set up. The other order works too: plugins can add health checks and telemetry sources before the service defaults exist.

## Health checks of plugins

A plugin adds a health check like any application code. It appears under `/health` together with the checks of the service defaults:

```csharp
--8<-- "samples/SplitHosts/Acme.Orders/OrdersPlugin.cs:plugin"
```

The plugin above also adds its own `ActivitySource` with `ConfigureOpenTelemetryTracerProvider`. That method comes from the package `OpenTelemetry.Api.ProviderBuilderExtensions`, which the plugin references; it works whether the host sets up OpenTelemetry before or after the plugin.

## Telemetry of Plugwright

Plugwright writes all log entries with the category `Plugwright`, so you can filter them in the dashboard. It also has a meter and an activity source, both named `Plugwright`. Add them in the service defaults:

```csharp
--8<-- "samples/SplitHosts/ServiceDefaults/Extensions.cs:telemetry"
```

- The meter reports the number of plugins per state as `plugwright.plugins`.
- The activity source traces [startup tasks](run-startup-tasks.md). Startup tasks run before the hosted services start, so their activities are only recorded if a listener exists at that time.

See [Log events and telemetry](../reference/log-events.md).

## See also

- [Split a plugin across an API host and a UI host](split-plugin-across-hosts.md)
- [Inspect plugins at runtime](inspect-plugins-at-runtime.md)
