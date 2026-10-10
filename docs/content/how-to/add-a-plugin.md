# Add a plugin to your application

This guide shows how to write a plugin that adds a hosted service with its own settings, and how to add it to a host application. The complete, runnable project is in `samples/Heartbeat` in the repository.

## Write the plugin

A plugin implements [`IPlugin`](../api/Plugwright.IPlugin.md). Its [`IPlugin.Configure`](../api/Plugwright.IPlugin.md#Plugwright_IPlugin_Configure_Plugwright_PluginContext_) method registers services through the builder in the [`PluginContext`](../api/Plugwright.PluginContext.md):

```csharp
--8<-- "samples/Heartbeat/HeartbeatPlugin.cs:plugin"
```

The plugin runs before the service container exists. It cannot receive services through its constructor and needs a public parameterless constructor. Runtime behavior comes from the services it registers, here a hosted service:

```csharp
--8<-- "samples/Heartbeat/HeartbeatService.cs:service"
```

## Read settings

[`PluginContext.Configuration`](../api/Plugwright.PluginContext.md#Plugwright_PluginContext_Configuration) is the plugin's own settings section, `Plugwright:Plugins:{PluginId}:Settings`. Bind it with the options pattern, as the plugin above does:

```csharp
--8<-- "samples/Heartbeat/HeartbeatOptions.cs:options"
```

The plugin ID is the name of the plugin's assembly. For the sample it is `Heartbeat`, so the settings live here:

```json
--8<-- "samples/Heartbeat/appsettings.json"
```

Plugin settings are normal configuration. Environment variables and command-line arguments work as usual, for example `Plugwright__Plugins__Heartbeat__Settings__Interval=00:00:10`.

All plugin classes in one assembly share the same plugin ID and therefore the same settings section.

## Add the plugin to the host

Add the plugin type with `builder.Plugins.Add<T>()`, then call `builder.ApplyPlugins()` before `Build()` and `app.UsePlugins()` after it:

```csharp
--8<-- "samples/Heartbeat/Program.cs:host"
```

Both calls are required, in this order, and each exactly once. [The two phases](../explanation/two-phases.md) explains why.

When the application starts, Plugwright logs every loaded plugin:

```text
info: Plugwright[1000]
      Plugin Heartbeat 0.1.0.0 loaded (Heartbeat.HeartbeatPlugin, added in code)
```

## See also

- [`PluginHostApplicationBuilderExtensions`](../api/Plugwright.PluginHostApplicationBuilderExtensions.md)
- [Configuration reference](../reference/configuration.md)
