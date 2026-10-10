# Support your own kind of plugin with a handler

`builder.Plugins.Add<T>()` only records a plugin type. A *plugin handler* decides what happens with it. The built-in handler processes every type that implements [`IPlugin`](../api/Plugwright.IPlugin.md). Write your own handler when your application defines a different kind of plugin, for example plugins that contribute menu entries, report types or greetings.

The complete, runnable project is in `samples/CustomHandler` in the repository.

## Define the plugin contract

The host application defines what its plugins look like. Here, a plugin returns greetings:

```csharp
--8<-- "samples/CustomHandler/IGreetingPlugin.cs:contract"
```

```csharp
--8<-- "samples/CustomHandler/Greetings.cs:plugins"
```

## Write the handler

Derive the handler from [`PluginHandler<TPlugin>`](../api/Plugwright.PluginHandler-1.md), with your plugin interface as the type argument. The handler then processes every plugin type that implements the interface:

- [`PluginHandler<TPlugin>.Apply`](../api/Plugwright.PluginHandler-1.md#Plugwright_PluginHandler_1_Apply__0_Plugwright_PluginDescriptor_Microsoft_Extensions_Hosting_IHostApplicationBuilder_) runs in phase 1, before the host is built. Register services here.
- [`PluginHandler<TPlugin>.Use`](../api/Plugwright.PluginHandler-1.md#Plugwright_PluginHandler_1_Use__0_Plugwright_PluginDescriptor_Microsoft_Extensions_Hosting_IHost_) runs in phase 2, after the host is built. It is optional; the default implementation does nothing.

Both methods receive the plugin instance already typed as your interface:

```csharp
--8<-- "samples/CustomHandler/GreetingPluginHandler.cs:handler"
```

The [`PluginDescriptor`](../api/Plugwright.PluginDescriptor.md) gives the handler the plugin's type, its metadata and its [`PluginContext`](../api/Plugwright.PluginContext.md).

### Decide yourself which types to handle

When a handler needs a different rule than "implements this interface", implement [`IPluginHandler`](../api/Plugwright.IPluginHandler.md) directly. Then [`IPluginHandler.CanHandle`](../api/Plugwright.IPluginHandler.md#Plugwright_IPluginHandler_CanHandle_System_Type_) decides which types the handler processes, and [`PluginDescriptor.Instance`](../api/Plugwright.PluginDescriptor.md#Plugwright_PluginDescriptor_Instance) is an `object` that you cast yourself.

## Register the handler

Register the handler with `builder.PluginHandlers.Add<T>()` before calling `ApplyPlugins()`:

```csharp
--8<-- "samples/CustomHandler/Program.cs:host"
```

## Rules for handlers

- Handlers are created with their parameterless constructor, not resolved from dependency injection. The service container does not exist yet in phase 1.
- Every handler whose `CanHandle` returns `true` processes the plugin, in registration order. The built-in handler for `IPlugin` always runs first. A plugin can therefore implement `IPlugin` and your own interface, and both handlers process it.
- Plugwright creates exactly one instance of each plugin type. All handlers receive the same instance in both phases.
- A plugin type that no handler can handle causes a [`PluginException`](../api/Plugwright.PluginException.md), unless [failures are skipped](handle-plugin-failures.md).
- Handlers come from the host application. Plugins cannot register handlers.
