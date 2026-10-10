# Keep running when a plugin fails

By default, Plugwright stops the application with a [`PluginException`](../api/Plugwright.PluginException.md) when a plugin fails. This makes mistakes visible right away. If your application must start even when a plugin is broken, skip failing plugins and log an error instead, for all plugins or for single ones.

The complete, runnable project is in `samples/FailureHandling` in the repository.

## Skip failing plugins

Set [`PluginOptions.FailureMode`](../api/Plugwright.PluginOptions.md#Plugwright_PluginOptions_FailureMode) to [`PluginFailureMode.SkipAndLog`](../api/Plugwright.PluginFailureMode.md) before calling `ApplyPlugins()`:

```csharp
--8<-- "samples/FailureHandling/Program.cs:host"
```

With these plugins, `BrokenPlugin` is skipped and `WorkingPlugin` still runs:

```csharp
--8<-- "samples/FailureHandling/Plugins.cs:plugins"
```

```text
fail: Plugwright[1001]
      Plugin FailureHandling (added in code) skipped: Phase 1 failed for plugin type 'FailureHandling.BrokenPlugin' in handler 'Plugwright.DefaultPluginHandler': The database connection string is missing.
      System.InvalidOperationException: The database connection string is missing.
info: Plugwright[1000]
      Plugin FailureHandling 0.1.0 loaded (FailureHandling.WorkingPlugin, added in code)
warn: Plugwright[1008]
      Plugin FailureHandling 0.1.0 (added in code): Failed. Phase 1 failed for plugin type 'FailureHandling.BrokenPlugin' in handler 'Plugwright.DefaultPluginHandler': The database connection string is missing.
```

Both plugin classes are in one assembly and share the plugin ID `FailureHandling`. The plugin counts as failed, but its other class keeps running.

## Choose per plugin

`FailureMode` is the default for all plugins. A plugin's [`PluginPolicy`](../api/Plugwright.PluginPolicy.md) overrides it, for example to skip one optional plugin while all others stop the application, and required plugins always stop it. See [Required and optional plugins](required-and-optional-plugins.md).

## What counts as a failure

The failure mode covers failures in phase 1 and in startup tasks:

- An exception in the plugin's constructor.
- An exception in [`IPlugin.Configure`](../api/Plugwright.IPlugin.md#Plugwright_IPlugin_Configure_Plugwright_PluginContext_) or in a handler's `Apply`.
- A plugin type that no handler can handle.
- A plugin folder that is broken: its main assembly is missing or cannot be loaded, or it contains no plugin type. See [Load plugins from a directory](load-plugins-from-a-directory.md).
- A plugin that fails [verification](verify-plugins-with-a-lock-file.md), or that no registered verifier can verify.
- A dependency that is missing, too old or part of a cycle. See [Declare plugin dependencies](declare-plugin-dependencies.md).
- An exception in a [startup task](run-startup-tasks.md).

Plugins that depend on a skipped plugin are skipped as well, each with its own log entry. A plugin that is [turned off](enable-and-disable-plugins.md) is no failure: plugins that depend on it are not loaded, whatever the failure mode.

Two kinds of errors always throw:

- Exceptions in a handler's `Use` (phase 2).
- The same plugin ID from two sources. Plugwright cannot know which one you want.

## What a skipped plugin leaves behind

When a plugin fails in phase 1, Plugwright removes the services that its plugin class registered before the failure. Some changes cannot be taken back and stay:

- configuration sources the plugin added,
- entries in the builder's `Properties`,
- callbacks registered with `ConfigureContainer`.

When a plugin fails in a startup task, the application is already built and its services stay registered. Check the plugin's state in the catalog where that matters.

## See what was skipped

Every skipped plugin produces a log entry with event ID 1001 and level `Error`, and every plugin its state with event ID 1008. See [Log events and telemetry](../reference/log-events.md).

The [`IPluginCatalog`](../api/Plugwright.IPluginCatalog.md) in the service container lists all plugins with their state, for example for a status page:

```csharp
--8<-- "samples/FailureHandling/Program.cs:report"
```

See [Inspect plugins at runtime](inspect-plugins-at-runtime.md).
