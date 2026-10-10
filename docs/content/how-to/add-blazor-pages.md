# Add Blazor pages from plugins

A plugin can bring Razor components, routable pages included, to a Blazor Web App with interactive server rendering. The plugin is a Razor class library; the host makes its assemblies known to the router.

The complete, runnable project is in `samples/BlazorHost` in the repository: a Blazor host without API and database, and a plugin that brings the page `/orders`, a CSS file and a menu entry.

## Create the plugin

Create a Razor class library that references `Plugwright.AspNetCore.Abstractions`:

```xml
--8<-- "samples/BlazorHost/Acme.Orders.Ui/Acme.Orders.Ui.csproj"
```

Mark the plugin with [`IBlazorPlugin`](../api/Plugwright.IBlazorPlugin.md). It has no members; it tells the host that the plugin's assembly contains components. Register services as usual in `Configure`:

```csharp
--8<-- "samples/BlazorHost/Acme.Orders.Ui/OrdersUiPlugin.cs:plugin"
```

Pages are ordinary Razor components with `@page`:

```razor
--8<-- "samples/BlazorHost/Acme.Orders.Ui/Pages/Orders.razor"
```

## Set up the host

Register [`BlazorPluginHandler`](../api/Plugwright.BlazorPluginHandler.md) and pass the plugin assemblies to `MapRazorComponents` with `AddPluginAssemblies(app)`:

```csharp
--8<-- "samples/BlazorHost/App/Program.cs:host"
```

`AddPluginAssemblies(app)` comes from Plugwright.AspNetCore. It adds the assemblies of all loaded Blazor plugins as additional assemblies, so that their pages are reachable by URL. The Blazor handler needs neither MVC nor the web plugin handler.

## Pass the assemblies to the router

How you set up the router depends on where it runs:

- **Interactivity per page or component** (the default of the Blazor template, as in the sample): the router renders on the server, and `AddPluginAssemblies(app)` is enough. Pages of plugins are found by direct call and by navigation.
- **Global interactivity** (`<Routes @rendermode="InteractiveServer" />`): the router runs in the circuit and matches routes itself. Then it needs the plugin assemblies too, or navigation to a plugin page ends on "not found".

To cover both, inject [`IPluginComponentAssemblies`](../api/Plugwright.IPluginComponentAssemblies.md) into the router:

```razor
--8<-- "samples/BlazorHost/App/Components/Routes.razor:router"
```

`IPluginComponentAssemblies` is always registered when the Blazor handler is, even if no Blazor plugin is loaded, for example because it is [turned off](enable-and-disable-plugins.md).

## Static files

Files in the plugin's `wwwroot` folder are served under `_content/<assembly name>/`, as for every Razor class library that the host references. The page above links its CSS file this way.

!!! note

    This works for plugins that the host references as a project or package. For plugins loaded from a directory or a package folder at runtime, the host does not know their static files yet.

## Menus and other extension points

Plugwright has no types for menus, navigation entries or page sections: those are contracts of your application. The sample defines a `NavigationItem` in a small project that host and plugins share, and the plugin registers an entry as a service. See [Offer extension points to plugins](offer-extension-points.md).

## See also

- [Use plugins in an ASP.NET Core app](use-in-a-web-app.md)
- [Split a plugin across an API host and a UI host](split-plugin-across-hosts.md)
- [Develop plugins in a solution](develop-plugins-in-a-solution.md), including Hot Reload for Razor components
