# Load contexts and shared assemblies

Plugins from a directory are not referenced by the host application. Plugwright loads them at runtime, each into its own <xref:System.Runtime.Loader.AssemblyLoadContext>. This page explains what that means for the assemblies a plugin uses.

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
- `Microsoft.Extensions.*`

This holds even when the plugin folder contains its own copy. The host's version is used, whatever version the plugin ships.

> [!NOTE]
> If a plugin was compiled against a newer version of a `Microsoft.Extensions.*` package than the host provides, it can fail at runtime with a `MissingMethodException` or `TypeLoadException`. Keep the host's `Microsoft.Extensions.*` packages at least as new as those of your plugins.

If the host does not have one of these assemblies at all, for example a `Microsoft.Extensions.*` package that only the plugin uses, the plugin's own copy is loaded.

Framework assemblies such as `System.Runtime` come from the shared .NET runtime, as for every application.

## No unloading

Load contexts are not collectible. A loaded plugin stays loaded until the process ends. Adding, removing or updating a plugin takes effect after a restart of the application.

Unloading would rarely work in a dependency injection application anyway. The container, hosted services and many libraries keep references to plugin types, and a load context can only be unloaded when no references remain.

## Loading runs no plugin code

Plugwright reads the metadata of a plugin folder, including its dependencies, with a <xref:System.Reflection.MetadataLoadContext>. That only reads the assembly file; nothing is loaded for execution. A plugin with a missing dependency is therefore rejected before any of its code is loaded.

Loading a plugin assembly and finding its plugin types does not call into the plugin either. Plugin code first runs when Plugwright creates the plugin in phase 1.
