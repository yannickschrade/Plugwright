# Log events and telemetry

Plugwright logs with the category `Plugwright` and fixed event IDs. Use them to filter or alert on plugin events. Event IDs do not change between versions.

| Event ID | Name | Level | When |
| --- | --- | --- | --- |
| 1000 | `PluginLoaded` | Information | A plugin completed both phases. Written by `app.UsePlugins()`. |
| 1001 | `PluginSkipped` | Error | A plugin failed and was skipped because [`PluginOptions.FailureMode`](../api/Plugwright.PluginOptions.md#Plugwright_PluginOptions_FailureMode) is [`PluginFailureMode.SkipAndLog`](../api/Plugwright.PluginFailureMode.md). Includes the exception. |
| 1002 | `PluginDirectoryNotFound` | Warning | A directory added with `AddFromDirectory()` does not exist. |
| 1003 | `AssemblyOutsidePluginFolder` | Warning | A DLL lies directly in a plugin directory instead of in a plugin folder, and is ignored. |
| 1004 | `PluginVerificationDisabled` | Information | Plugins are loaded from a directory or a package folder, but no verifier is registered. |
| 1005 | `PluginVerified` | Information | A plugin passed a verifier. One entry per plugin and verifier. |
| 1006 | `PluginVerificationFailed` | Error | A plugin failed a verifier. Includes the exception if the verifier threw one. |
| 1007 | `PackageDownloaded` | Information | A library package was downloaded from the feed of a package folder (Plugwright.NuGet). |
| 1008 | `PluginStatus` | Information or Warning | The state of a plugin at startup, one entry per plugin. Information for `Loaded` and `Disabled`, a warning otherwise. Written by `app.UsePlugins()`. |
| 1009 | `PluginStartupCompleted` | Information | A [startup task](../how-to/run-startup-tasks.md) completed. |
| 1010 | `PluginStartupFailed` | Error | A startup task threw. Includes the exception. |
| 1011 | `PluginStartupSkipped` | Warning | A startup task did not run because its plugin is not loaded, for example because a dependency failed in its own startup task. |

## Structured values

| Event | Values |
| --- | --- |
| `PluginLoaded` | `PluginId`, `PluginVersion`, `PluginType`, `PluginSource` |
| `PluginSkipped` | `PluginId`, `PluginSource`, `Reason` |
| `PluginDirectoryNotFound` | `PluginDirectory` |
| `AssemblyOutsidePluginFolder` | `AssemblyPath`, `PluginDirectory` |
| `PluginVerificationDisabled` | – |
| `PluginVerified`, `PluginVerificationFailed` | `PluginName`, `PluginLocation`, `Verifier`, `Result` |
| `PackageDownloaded` | `PackageId`, `PackageVersion`, `Feed`, `PackagePath` |
| `PluginStatus` | `PluginId`, `PluginVersion`, `PluginSource`, `PluginState`, `Reason` |
| `PluginStartupCompleted`, `PluginStartupFailed` | `PluginId`, `StartupTask`, `ElapsedMilliseconds` |
| `PluginStartupSkipped` | `PluginId`, `StartupTask`, `Reason` |

`PluginSource` is `added in code` for plugins added with `Add<T>()`, and the full path of the plugin folder for plugins loaded from a directory.

Entries from phase 1 are buffered and written when `app.UsePlugins()` runs. See [The two phases](../explanation/two-phases.md).

## Metrics

The meter `Plugwright` has one instrument:

| Instrument | Type | Unit | Tags | Value |
| --- | --- | --- | --- | --- |
| `plugwright.plugins` | Observable gauge | `{plugin}` | `plugwright.plugin.state` | The number of plugins in each [state](../api/Plugwright.PluginState.md). |

The meter is created through the host's `IMeterFactory` in `app.UsePlugins()`. With OpenTelemetry, add it with `AddMeter("Plugwright")`.

## Traces

The activity source `Plugwright` starts one activity `Plugwright.Startup` per startup task, with the tags `plugwright.plugin.id` and `plugwright.startup.type`, and the status `Error` if the task fails. With OpenTelemetry, add it with `AddSource("Plugwright")`. Startup tasks run before hosted services start, so their activities are only recorded by listeners that exist at that time.
