# Required and optional plugins

Some plugins are essential: the application must not run without them. Others are nice to have: the application runs without them and shows that they are missing. Plugwright lets the host decide this for each plugin.

The complete, runnable project is in `samples/PluginPolicies` in the repository.

## Set the policy of each plugin

```csharp
--8<-- "samples/PluginPolicies/App/Program.cs:policies"
```

- [`PluginOptions.FailureMode`](../api/Plugwright.PluginOptions.md#Plugwright_PluginOptions_FailureMode) is the default for all plugins: `FailFast` (the default) stops the application when a plugin fails, `SkipAndLog` skips it.
- [`Require(pluginId)`](../api/Plugwright.PluginCollection.md#Plugwright_PluginCollection_Require_System_String_) marks a plugin as required. It works for plugins from any source, so also for plugins that a directory or package folder provides.
- `Add<T>(policy => …)` adds a plugin type and sets the [`PluginPolicy`](../api/Plugwright.PluginPolicy.md) of its plugin. [`Configure(pluginId, policy => …)`](../api/Plugwright.PluginCollection.md#Plugwright_PluginCollection_Configure_System_String_System_Action_Plugwright_PluginPolicy__) does the same by ID for plugins from any source.
- A plugin's `FailureMode` overrides the default, in both directions.

The sample plugin `Acme.Reports` fails on purpose, so the application runs without it:

```text
warn: Plugwright[1008]
      Plugin Acme.Reports 0.1.0 (added in code): Failed. Phase 1 failed for plugin type 'Acme.Reports.ReportsPlugin' in handler 'Plugwright.DefaultPluginHandler': The report archive is not reachable.
Acme.Billing: Loaded (required)
Acme.Reports: Failed Phase 1 failed for plugin type 'Acme.Reports.ReportsPlugin' in handler 'Plugwright.DefaultPluginHandler': The report archive is not reachable.
```

## What required means

A required plugin stops the application with a [`PluginException`](../api/Plugwright.PluginException.md), whatever the failure mode, when it

- is not provided by any source,
- is [turned off](enable-and-disable-plugins.md) in configuration,
- depends on a plugin that is missing, off or failed,
- fails verification,
- throws in its constructor, in `Configure` or in a handler,
- or fails in a [startup task](run-startup-tasks.md).

For example, the sample with `Plugwright__Plugins__Acme.Billing__Enabled=false`:

```text
Unhandled exception. Plugwright.PluginException: Required plugin 'Acme.Billing' is turned off in configuration (Plugwright:Plugins:Acme.Billing:Enabled). Required plugins cannot be turned off.
```

The exception names all required plugins with problems at once. With several, the message starts with `Required plugins cannot start:` and lists each on its own line.

Problems that are known before any plugin runs, such as a missing or turned-off plugin, stop the application before the first `Configure`. Failures in `Configure` are collected until all plugins have been configured.

!!! warning "Important"

    Only code decides which plugins are required. Configuration cannot change it, so nobody can turn off an essential plugin by editing a settings file.

## What happens to a skipped plugin

When a plugin with `SkipAndLog` fails while it is configured, Plugwright takes back the services it registered. The application runs as if the plugin were not there; in the sample, `ReportArchive` is not registered. Plugins that depend on it are skipped too. See [Keep running when a plugin fails](handle-plugin-failures.md).

## See also

- [Keep running when a plugin fails](handle-plugin-failures.md)
- [Inspect plugins at runtime](inspect-plugins-at-runtime.md)
- [Plugin states](../explanation/plugin-states.md)
