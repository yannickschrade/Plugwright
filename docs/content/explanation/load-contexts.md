# Load contexts and shared assemblies

Plugins from a directory are not referenced by the host application. Plugwright loads them at runtime, each into its own [`AssemblyLoadContext`](https://learn.microsoft.com/dotnet/api/system.runtime.loader.assemblyloadcontext). This page explains what that means for the assemblies a plugin uses.

## One load context per plugin

Every plugin folder gets its own load context, named after the plugin ID. The plugin's main assembly and its private dependencies are loaded into it, using the plugin's `.deps.json` to find them.

This isolates plugins from each other. Two plugins can ship different versions of the same library, and each one gets its own copy, side by side in the same process.

## Plugins that depend on each other

When a plugin depends on another plugin, its load context takes the other plugin's assembly from the other plugin's load context. Both use the same types, so services registered by one plugin can be injected into the services of the other. This also holds when the dependent plugin's folder contains its own copy of the other plugin's assembly; that copy is ignored. See [Declare plugin dependencies](../how-to/declare-plugin-dependencies.md).

## Plugins from packages

Plugin packages get their own load context as well, named after the package ID. Plugwright reads each `.nupkg` file into memory once and loads the assemblies of the plugin and of its library packages from there; nothing is extracted to disk. The assemblies come from the `lib` folder that fits .NET 10, and assemblies for the current runtime (`runtimes/<rid>/lib/`) take precedence.

Native libraries (`runtimes/<rid>/native/`) are the exception, because the operating system loads them from disk. They are written to a temporary folder of the process when a plugin first needs them, checked against the package content right before loading, and the folder is deleted when the process exits.

Library packages are loaded into the load context of every plugin package that depends on them. Two plugin packages can therefore use different versions of the same library.

## Shared assemblies come from the host

Some types must be the same for the host and all plugins. If a plugin loaded its own copy of `IPlugin` or `IServiceCollection`, the host would not recognize the plugin, and services registered by the plugin would not match the host's container.

Plugwright therefore always takes these assemblies from the host, from the default load context:

- `Plugwright.Abstractions` and `Plugwright`
- `Plugwright.AspNetCore.Abstractions` and `Plugwright.AspNetCore`
- `Microsoft.Extensions.*`
- `Microsoft.AspNetCore.*`, so that web plugins and the host share controllers, endpoints and components

This holds even when the plugin folder contains its own copy. The host's version is used, whatever version the plugin ships.

!!! note

    If a plugin was compiled against a newer version of a `Microsoft.Extensions.*` package than the host provides, it can fail at runtime with a `MissingMethodException` or `TypeLoadException`. Keep the host's `Microsoft.Extensions.*` packages at least as new as those of your plugins.

If the host does not have one of these assemblies at all, for example a `Microsoft.Extensions.*` package that only the plugin uses, the plugin's own copy is loaded.

Framework assemblies such as `System.Runtime` come from the shared .NET runtime, as for every application.

## Share your own assemblies

Contracts of your own application, for example for menu entries, UI component libraries or frameworks whose types plugins exchange with the host, such as Entity Framework Core, must be shared as well. Otherwise a plugin that ships its own copy uses its own types, and the host does not find the services the plugin registers.

Share them in the host, before `ApplyPlugins()`:

- [`ShareAssembly`](../api/Plugwright.PluginCollection.md#Plugwright_PluginCollection_ShareAssembly_System_Reflection_Assembly_) shares one assembly of the host, usually `typeof(SomeType).Assembly`.
- [`ShareAssemblies`](../api/Plugwright.PluginCollection.md#Plugwright_PluginCollection_ShareAssemblies_System_String_) shares all assemblies whose names start with a prefix. The prefix only matches at a dot: `Microsoft.EntityFrameworkCore` shares `Microsoft.EntityFrameworkCore` and `Microsoft.EntityFrameworkCore.SqlServer`, but not `Microsoft.EntityFrameworkCoreX`.

```csharp
--8<-- "samples/SharedAssemblies/App/Program.cs:host"
```

The same rules apply as for the built-in shared assemblies: the host's version wins, whatever version the plugin ships, and an assembly the host does not have is loaded from the plugin. Sharing applies to all plugins from directories and packages.

Alternatively, plugins can leave the contract out of their folder: reference it with `Private="false" ExcludeAssets="runtime"`, like the plugin contract. Then the plugin's load context finds no private copy and uses the host's. Sharing in the host also covers plugins that do ship a copy, for example because the contract comes from a NuGet package. See [Offer extension points to plugins](../how-to/offer-extension-points.md).

## No unloading

Load contexts are not collectible. A loaded plugin stays loaded until the process ends. Adding, removing or updating a plugin takes effect after a restart of the application.

Unloading would rarely work in a dependency injection application anyway. The container, hosted services and many libraries keep references to plugin types, and a load context can only be unloaded when no references remain.

## Loading runs no plugin code

Plugwright reads the metadata of a plugin folder, including its dependencies, with a [`MetadataLoadContext`](https://learn.microsoft.com/dotnet/api/system.reflection.metadataloadcontext). That only reads the assembly file; nothing is loaded for execution. A plugin with a missing dependency is therefore rejected before any of its code is loaded.

Loading a plugin assembly and finding its plugin types does not call into the plugin either. Plugin code first runs when Plugwright creates the plugin in phase 1.
