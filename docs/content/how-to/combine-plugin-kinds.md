# One plugin, several kinds

A plugin class can implement several plugin contracts at once, for example [`IPlugin`](../api/Plugwright.IPlugin.md) for services, [`IWebPlugin`](../api/Plugwright.IWebPlugin.md) for controllers, endpoints and middleware, and [`IBlazorPlugin`](../api/Plugwright.IBlazorPlugin.md) for pages. Every handler that can handle the class processes it.

## Write the plugin

This plugin from the integration tests of Plugwright implements all three contracts:

```csharp
--8<-- "tests/TestPlugins/Web/WebPlugin.cs:plugin"
```

Its assembly also contains a controller and a page, which the web and Blazor handlers find without further code.

## Register the handlers

The host registers the handler for each kind it supports. The handler for `IPlugin` is always registered:

```csharp
--8<-- "tests/TestWebHost/Program.cs:handlers"
```

## How the handlers work together

- All handlers that can handle a plugin class process it, in registration order, the handler for `IPlugin` first. So services from `Configure` exist when the other handlers run.
- The plugin system creates one instance of the class and passes it to all handlers in both phases. State from `Configure` is available in `ConfigurePipeline` and `MapEndpoints`.
- If one handler fails for the plugin, the plugin fails. See [Keep running when a plugin fails](handle-plugin-failures.md).
- A class needs no `IPlugin`: a class that only implements `IWebPlugin` is a plugin as soon as the web handler is registered.

## One plugin or several?

Combine the kinds when the plugin runs in one host that serves API and pages. If backend and UI run in different hosts, split the plugin into a backend plugin and a UI plugin with their own IDs. See [Split a plugin across an API host and a UI host](split-plugin-across-hosts.md).

## See also

- [Use plugins in an ASP.NET Core app](use-in-a-web-app.md)
- [Add Blazor pages from plugins](add-blazor-pages.md)
- [Write a plugin handler](write-a-plugin-handler.md)
