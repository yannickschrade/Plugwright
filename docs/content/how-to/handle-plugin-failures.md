# Keep running when a plugin fails

By default, Plugwright stops the application with a [`PluginException`](../api/Plugwright.PluginException.md) when a plugin fails. This makes mistakes visible right away. If your application must start even when a plugin is broken, skip failing plugins and log an error instead.

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
      Plugin FailureHandling 0.1.0.0 loaded (FailureHandling.WorkingPlugin, added in code)
```

## What counts as a failure

`SkipAndLog` covers failures in phase 1:

- An exception in the plugin's constructor.
- An exception in [`IPlugin.Configure`](../api/Plugwright.IPlugin.md#Plugwright_IPlugin_Configure_Plugwright_PluginContext_) or in a handler's `Apply`.
- A plugin type that no handler can handle.
- A plugin folder that is broken: its main assembly is missing or cannot be loaded, or it contains no plugin type. See [Load plugins from a directory](load-plugins-from-a-directory.md).
- A plugin that fails [verification](verify-plugins-with-a-lock-file.md), or that no registered verifier can verify.
- A dependency that is missing, too old or part of a cycle. See [Declare plugin dependencies](declare-plugin-dependencies.md).

Plugins that depend on a skipped plugin are skipped as well, each with its own log entry.

Two kinds of errors always throw:

- Exceptions in a handler's `Use` (phase 2).
- The same plugin ID from two sources. Plugwright cannot know which one you want.

!!! warning

    Services that a plugin registered before it failed stay registered. A plugin that fails halfway through `Configure` can leave an incomplete set of services behind. Register services at the end of `Configure`, after everything that can fail.

## See what was skipped

Every skipped plugin produces a log entry with event ID 1001 and level `Error`. See [Log events](../reference/log-events.md).

The [`PluginReport`](../api/Plugwright.PluginReport.md) in the service container lists the active and the skipped plugins, and the results of verification, for example for a status page:

```csharp
--8<-- "samples/FailureHandling/Program.cs:report"
```
