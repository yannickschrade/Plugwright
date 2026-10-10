# Inspect plugins at runtime

The plugin catalog knows every plugin the application found, also those that are not loaded, with metadata and state. Use it for status pages, for decisions in your own code, and to tell other hosts which plugins run.

## Read the catalog

[`IPluginCatalog`](../api/Plugwright.IPluginCatalog.md) is a singleton in the service container after `builder.ApplyPlugins()`. It lives in `Plugwright.Abstractions`, so plugins can use it as well. `samples/BlazorHost` shows it as a table:

```razor
--8<-- "samples/BlazorHost/App/Components/Pages/Plugins.razor:catalog"
```

Each entry is a [`PluginInfo`](../api/Plugwright.PluginInfo.md): one per plugin ID, so several plugin classes of one assembly share an entry. Besides ID, version and state it contains the source, the reason and error message for plugins that are not loaded, whether the host requires the plugin, its dependencies and the handlers that processed it.

[`Find(pluginId)`](../api/Plugwright.IPluginCatalog.md#Plugwright_IPluginCatalog_Find_System_String_) returns a single plugin, compared without case:

```razor
--8<-- "samples/SplitHosts/Web/Components/App.razor:nav"
```

## Plugin states

| [`PluginState`](../api/Plugwright.PluginState.md) | Meaning |
| --- | --- |
| `Loaded` | The plugin is loaded and all its handlers ran. |
| `Disabled` | The plugin is [turned off](enable-and-disable-plugins.md) in configuration. |
| `DependencyUnavailable` | A plugin it depends on is missing, has the wrong version or is not loaded. The reason names it. |
| `Failed` | The plugin could not be read or loaded, or it threw while it was configured or in a startup task. |
| `VerificationFailed` | The plugin did not pass [verification](verify-plugins-with-a-lock-file.md). |
| `NoHandler` | No registered handler can handle the plugin's types. |

States are final after startup, with one exception: a plugin whose [startup task](run-startup-tasks.md) fails becomes `Failed`, and plugins that depend on it become `DependencyUnavailable`. The catalog is safe to read from several threads; [`Plugins`](../api/Plugwright.IPluginCatalog.md#Plugwright_IPluginCatalog_Plugins) returns a snapshot.

See [Plugin states](../explanation/plugin-states.md) for how the states come about.

## Logs and metrics

- At startup, every plugin's state is logged with event ID 1008 (`PluginStatus`): information for `Loaded` and `Disabled`, a warning otherwise.
- The meter `Plugwright` reports the number of plugins per state as the gauge `plugwright.plugins`, tagged with `plugwright.plugin.state`.

See [Log events and telemetry](../reference/log-events.md).

## Publish the catalog to other hosts

A host can publish its catalog as JSON, so that other hosts learn which plugins run there. The API host in `samples/SplitHosts` does this with `app.MapPluginCatalog()` from Plugwright.AspNetCore:

```csharp
--8<-- "samples/SplitHosts/Api/Program.cs:host"
```

The endpoint answers `GET /_plugwright/plugins` (the route is a parameter) with a list of [`PluginStatus`](../api/Plugwright.PluginStatus.md):

```json
[{"id":"Acme.Orders","version":"0.1.0","state":"Disabled","reason":"It is turned off in configuration (Plugwright:Plugins:Acme.Orders:Enabled)."}]
```

- `PluginStatus` is in `Plugwright.Abstractions`, so a client reads the answer without referencing the main package. [Split a plugin across an API host and a UI host](split-plugin-across-hosts.md) shows such a client.
- Error messages of failed plugins are left out, because they can reveal details of the application. Pass `includeErrorMessages: true` to include them. Stack traces are never included.
- The endpoint only reads. It never turns plugins on or off.

!!! warning "Important"

    The endpoint is not mapped automatically, but once mapped it is open to everyone who can reach the application. Protect it, for example with `.RequireAuthorization()`, when the application is reachable from outside.

## Catalog or report?

[`PluginReport`](../api/Plugwright.PluginReport.md) is for the host application only: it holds the loaded plugin types with their instances, and the result of every verification. The catalog is the stable view of all plugins and their state, for hosts and plugins alike.

## See also

- [Keep running when a plugin fails](handle-plugin-failures.md)
- [Use Plugwright with .NET Aspire](use-with-aspire.md)
