# The two phases

Every application that uses Plugwright calls the plugin system at two fixed places:

[!code-csharp[](../../samples/Heartbeat/Program.cs?name=host)]

| Call | When | What happens |
| --- | --- | --- |
| `builder.Plugins.Add…()` | Before phase 1 | Records plugin types and directories. Nothing is loaded, created or run yet. |
| `builder.ApplyPlugins()` | Phase 1, before `Build()` | Reads plugin metadata, checks dependencies, loads plugin assemblies in dependency order, creates each plugin once and lets the handlers register services. |
| `app.UsePlugins()` | Phase 2, after `Build()` | Writes the log entries from phase 1 and lets the handlers work with the built host. |

## Why two explicit calls

`Build()` belongs to the builder you use: <xref:Microsoft.Extensions.Hosting.HostApplicationBuilder>, `WebApplicationBuilder` or another one. Plugwright adds members to these builders but cannot run code inside their `Build()` method. Some plugin systems hook into the build through a custom `IServiceProviderFactory`. That conflicts with containers such as Autofac and hides what happens. Plugwright asks you to make both calls instead.

Two calls also give you control over order. In a web application, the position of `app.UsePlugins()` decides where plugin middleware ends up in the pipeline. And the pattern matches the familiar .NET convention of `AddX` and `UseX`.

## Phase 1: before the container exists

During phase 1 the service container does not exist yet. That has three consequences:

- **Plugins and handlers have parameterless constructors.** They cannot receive services through their constructor. Plugins register services, and those services receive their dependencies as usual.
- **Logging is buffered.** <xref:Plugwright.PluginContext.Logger> and Plugwright's own log entries are collected during phase 1 and written to the configured logging providers when `app.UsePlugins()` runs. After that, the plugin's logger writes directly.
- **Fail-fast errors are self-contained.** If phase 1 stops with a <xref:Plugwright.PluginException>, the buffered entries have not been written. The exception message names the plugin and the cause, so nothing is lost.

## Plugins and handlers

Plugins describe *what* extends the application. Handlers decide *how*. The built-in handler processes <xref:Plugwright.IPlugin> and calls `Configure`. Other kinds of plugins, for example web plugins with middleware, come as additional handlers without changing the core. See [Write a plugin handler](../how-to/write-a-plugin-handler.md).

## Mistakes the plugin system catches

Plugwright throws an <xref:System.InvalidOperationException> with an explanation when:

- `app.UsePlugins()` is called without `builder.ApplyPlugins()` before `Build()`.
- `builder.ApplyPlugins()` or `app.UsePlugins()` is called twice.
- Plugins or handlers are added after `builder.ApplyPlugins()`.

## Trust

Plugins run with full trust inside your process. A plugin can read every configuration value, call every service and access the file system with the rights of your application. Only load plugins you trust.
