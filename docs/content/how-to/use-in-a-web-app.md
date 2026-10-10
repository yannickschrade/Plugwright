# Use plugins in an ASP.NET Core app

Plugwright works with `WebApplicationBuilder` and `WebApplication` the same way as with a console host. With the web handler from Plugwright.AspNetCore, plugins also bring MVC controllers, minimal API endpoints and middleware.

The complete, runnable project is in `samples/WebHost` in the repository.

## Set up the host

Add the package `Plugwright.AspNetCore` to the web application, register the web plugin handler and call the two phases as usual:

```csharp
--8<-- "samples/WebHost/App/Program.cs:host"
```

- `builder.Plugins`, `builder.ApplyPlugins()` and `app.UsePlugins()` are the same members as in every host. `WebApplicationBuilder` is an `IHostApplicationBuilder`, and `WebApplication` an `IHost`.
- [`WebPluginHandler`](../api/Plugwright.AspNetCore.WebPluginHandler.md) processes every plugin that implements [`IWebPlugin`](../api/Plugwright.AspNetCore.IWebPlugin.md). Register it before `ApplyPlugins()`.
- The web types, such as `WebPluginHandler` and `IWebPlugin`, are in the namespace `Plugwright.AspNetCore`. Extension members such as `AddPluginAssemblies` and `MapPluginCatalog` are in `Plugwright`, so they are found with the usual `using Plugwright;`.
- `AddControllers()` can come before or after `ApplyPlugins()`. The handler does not register MVC itself; if the host never calls `AddControllers()`, plugins can still bring endpoints and middleware.

## Write a web plugin

A web plugin implements [`IWebPlugin`](../api/Plugwright.AspNetCore.IWebPlugin.md) from the package `Plugwright.AspNetCore.Abstractions`. Both of its methods are optional:

```csharp
--8<-- "samples/WebHost/Acme.Greetings/GreetingsPlugin.cs:plugin"
```

- `Configure` runs in phase 1 and registers services, as in every plugin.
- [`ConfigurePipeline`](../api/Plugwright.AspNetCore.IWebPlugin.md#Plugwright_AspNetCore_IWebPlugin_ConfigurePipeline_Microsoft_AspNetCore_Builder_IApplicationBuilder_) and [`MapEndpoints`](../api/Plugwright.AspNetCore.IWebPlugin.md#Plugwright_AspNetCore_IWebPlugin_MapEndpoints_Microsoft_AspNetCore_Routing_IEndpointRouteBuilder_) run in phase 2, in `app.UsePlugins()`, plugin by plugin in dependency order.
- The plugin system creates one instance per plugin class. Fields set in `Configure` are still there in phase 2.

The plugin project references the ASP.NET Core shared framework:

```xml
--8<-- "samples/WebHost/Acme.Greetings/Acme.Greetings.csproj"
```

## Add controllers

Controllers in the plugin's assembly are found without further code. The handler adds the assembly as an MVC application part, also for plugins loaded from a directory or a package:

```csharp
--8<-- "samples/WebHost/Acme.Greetings/GreetingsController.cs:controller"
```

Controllers and endpoints of plugins appear in the OpenAPI document like those of the host, for example with `AddOpenApi()` and `MapOpenApi()`.

## Place the middleware

Plugin middleware runs where `app.UsePlugins()` stands in the pipeline. Middleware that the host adds before the call runs before the plugins' middleware, middleware after the call runs after it. Put `UsePlugins()` after the middleware the plugins rely on, such as authentication, and before the endpoints of the host.

Endpoints do not depend on that position: `MapEndpoints` adds endpoints to the routing of the application, wherever `UsePlugins()` stands.

## Use plugin services in the host

The host can use services a plugin registers, as `/` in the sample does. A plugin can be [turned off](enable-and-disable-plugins.md) or fail, though. Then its services are missing, and an endpoint that requires them makes the application fail at startup. Either handle the missing service, as the sample does, or make the plugin [required](required-and-optional-plugins.md).

## What can go wrong

`app.UsePlugins()` throws a [`PluginException`](../api/Plugwright.PluginException.md) if the host is not a web application but has a web plugin, for example a console host built with `Host.CreateApplicationBuilder()`.

## See also

- [Add Blazor pages from plugins](add-blazor-pages.md)
- [One plugin, several kinds](combine-plugin-kinds.md)
- [The two phases](../explanation/two-phases.md)
