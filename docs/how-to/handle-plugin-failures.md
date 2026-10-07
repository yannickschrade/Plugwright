# Keep running when a plugin fails

By default, Plugwright stops the application with a <xref:Plugwright.PluginException> when a plugin fails. This makes mistakes visible right away. If your application must start even when a plugin is broken, skip failing plugins and log an error instead.

The complete, runnable project is in `samples/FailureHandling` in the repository.

## Skip failing plugins

Set <xref:Plugwright.PluginOptions.FailureMode> to <xref:Plugwright.PluginFailureMode.SkipAndLog> before calling `ApplyPlugins()`:

[!code-csharp[](../../samples/FailureHandling/Program.cs?name=host)]

With these plugins, `BrokenPlugin` is skipped and `WorkingPlugin` still runs:

[!code-csharp[](../../samples/FailureHandling/Plugins.cs?name=plugins)]

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
- An exception in <xref:Plugwright.IPlugin.Configure(Plugwright.PluginContext)> or in a handler's `Apply`.
- A plugin type that no handler can handle.
- A plugin folder that is broken: its main assembly is missing or cannot be loaded, or it contains no plugin type. See [Load plugins from a directory](load-plugins-from-a-directory.md).
- A dependency that is missing, too old or part of a cycle. See [Declare plugin dependencies](declare-plugin-dependencies.md).

Plugins that depend on a skipped plugin are skipped as well, each with its own log entry.

Two kinds of errors always throw:

- Exceptions in a handler's `Use` (phase 2).
- The same plugin ID from two sources. Plugwright cannot know which one you want.

> [!WARNING]
> Services that a plugin registered before it failed stay registered. A plugin that fails halfway through `Configure` can leave an incomplete set of services behind. Register services at the end of `Configure`, after everything that can fail.

## Find skipped plugins in the logs

Every skipped plugin produces a log entry with event ID 1001 and level `Error`. See [Log events](../reference/log-events.md).
