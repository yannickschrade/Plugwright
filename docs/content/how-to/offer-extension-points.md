# Offer extension points to plugins

Menus, navigation entries, dashboard tiles, permissions, data types: what plugins can contribute to your application depends on the application. Plugwright does not define such contracts. Define them yourself and let plugins register implementations as services.

The example comes from `samples/BlazorHost` in the repository: plugins add entries to the host's menu.

## Define the contract

Put the contract in a small project that the host and the plugins reference, here `BlazorHost.Abstractions`. It contains no logic:

```csharp
--8<-- "samples/BlazorHost/BlazorHost.Abstractions/NavigationItem.cs:navigation"
```

The project is not a plugin. Plugins reference it like any library.

## Contribute from a plugin

A plugin registers its entries in `Configure`:

```csharp
--8<-- "samples/BlazorHost/Acme.Orders.Ui/OrdersUiPlugin.cs:plugin"
```

## Use the contributions in the host

The host injects all registered entries, as for every service with several implementations:

```razor
--8<-- "samples/BlazorHost/App/Components/Layout/NavMenu.razor"
```

A plugin that is turned off or fails contributes nothing, because its services are not registered.

## Things to keep in mind

- Version the contract carefully. A plugin compiled against an older contract must still load; add members instead of changing them.
- Plugins loaded from a directory or a package share the contract with the host only if they use the host's copy of its assembly. Share it in the host with `builder.Plugins.ShareAssembly(typeof(NavigationItem).Assembly)`, or reference the contract project in plugins with `Private="false" ExcludeAssets="runtime"`, as for `Plugwright.Abstractions`, so that the plugin folder contains no copy of its own. See [Share your own assemblies](../explanation/load-contexts.md#share-your-own-assemblies).
- Services are the extension mechanism for everything a plugin contributes at runtime. Plugwright only transports them; how the host uses them is up to the host.

## See also

- [Add Blazor pages from plugins](add-blazor-pages.md)
- [Write a plugin handler](write-a-plugin-handler.md), for contracts that the host processes once at startup
