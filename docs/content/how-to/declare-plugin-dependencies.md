# Declare plugin dependencies

A plugin can use services and types of another plugin. This guide shows how to declare such a dependency, so that Plugwright checks it, applies the plugins in the right order and lets both use the same types.

The complete, runnable project is in `samples/PluginDependencies` in the repository. `Acme.Welcome` uses a service that `Acme.Formatting` provides.

## What a dependency means

When plugin A depends on plugin B:

- **B must be present**, in the required version. Otherwise A fails before any plugin code is loaded.
- **B comes first.** Plugwright processes B before A in both phases, no matter in which order they were added or how their folders are named.
- **A and B share types.** When A uses a type from B, it gets the type from B's load context. Services that B registers can be injected into A's services.

## Provide something to depend on

The plugin depended on is an ordinary plugin with public types:

```csharp
--8<-- "samples/PluginDependencies/Acme.Formatting/GreetingFormatter.cs:formatting"
```

## Reference the other plugin

Reference the other plugin's project or package. Like the plugin contract, it is compiled against but not shipped, because it is deployed as its own plugin:

```xml
--8<-- "samples/PluginDependencies/Acme.Welcome/Acme.Welcome.csproj"
```

The reference alone is a dependency: Plugwright sees that `Acme.Welcome` references the assembly of the plugin `Acme.Formatting`. It also checks that the available assembly version is at least the one `Acme.Welcome` was compiled against.

The dependent plugin then uses the other plugin's types as usual:

```csharp
--8<-- "samples/PluginDependencies/Acme.Welcome/WelcomePlugin.cs:plugin"
```

## Require a minimum version

To require a minimum version, or to depend on a plugin you do not reference, add [`PluginDependencyAttribute`](../api/Plugwright.PluginDependencyAttribute.md) to the assembly:

```csharp
--8<-- "samples/PluginDependencies/Acme.Welcome/WelcomePlugin.cs:dependency"
```

`MinVersion` is a semantic version and is compared with the other plugin's version, the `<Version>` of its project. Prerelease versions are lower than the release: `1.2.0-beta` does not satisfy `MinVersion = "1.2.0"`. If both a reference and an attribute exist for the same plugin, both conditions must be met.

The attribute applies to the whole assembly, because all plugin classes in one assembly share their dependencies. If only one class of an assembly needs the other plugin, make the dependency [optional](#make-a-dependency-optional) and let that class check whether the other plugin is loaded. If the classes must start, stop and fail independently, put them in separate plugin assemblies. See [One plugin, several classes](../explanation/plugin-states.md#one-plugin-several-classes).

## Make a dependency optional

A plugin can use another plugin when it is installed and run without it otherwise. Set `IsOptional` on the attribute:

```csharp
--8<-- "samples/PluginDependencies/Acme.Farewell/FarewellPlugin.cs:dependency"
```

If the other plugin is present, an optional dependency works like a required one: it comes first, and both plugins use the same types. If it is missing, [turned off](enable-and-disable-plugins.md) or fails, the plugin is loaded anyway. A version that is too old is still an error, because it usually means a wrong installation.

Ask [`PluginContext.IsPluginLoaded`](../api/Plugwright.PluginContext.md#Plugwright_PluginContext_IsPluginLoaded_System_String_) in `Configure` and touch the other plugin's types only when it is loaded:

```csharp
--8<-- "samples/PluginDependencies/Acme.Farewell/FarewellPlugin.cs:plugin"
```

Things to keep in mind:

- The answer of `IsPluginLoaded` is final for plugins this plugin depends on, because they are processed first. At runtime, [`IPluginCatalog`](../api/Plugwright.IPluginCatalog.md) tells whether the other plugin is still loaded; it can fail later in a [startup task](run-startup-tasks.md).
- Code that uses a type of a plugin that is not loaded throws a `FileNotFoundException` when it first runs. Keep such code in separate classes or methods that only run when the plugin is loaded, as above. Plugwright does not load the other plugin's assembly from a copy in the plugin's own folder, so the behavior does not depend on how the plugin was built.
- A reference to the other plugin's assembly becomes optional through the attribute.
- In a NuGet package, declare optional dependencies only with the attribute. A dependency in the `.nuspec` is always required, and NuGet would install the other package anyway.
- [`PluginInfo.OptionalDependencies`](../api/Plugwright.PluginInfo.md#Plugwright_PluginInfo_OptionalDependencies) lists optional dependencies; `Dependencies` lists only required ones.

## When a dependency is not satisfied

| Situation | Message |
| --- | --- |
| The other plugin is missing. | `It depends on plugin 'Acme.Formatting', which is not available.` |
| The other plugin is too old. | `It requires plugin 'Acme.Formatting' version 1.2.0 or later, but version 1.1.0 is available.` |
| The plugin was compiled against a newer assembly version. | `It was compiled against assembly version 2.0.0.0 of plugin 'Acme.Formatting', but assembly version 1.0.0.0 is available.` |
| Plugins depend on each other in a circle. | `Plugins depend on each other in a cycle: Acme.A -> Acme.B -> Acme.A.` |

By default, each of these stops the application with a [`PluginException`](../api/Plugwright.PluginException.md). With [`PluginFailureMode.SkipAndLog`](../api/Plugwright.PluginFailureMode.md), the affected plugins are skipped instead, every plugin in a cycle included. Plugins that depend on a skipped plugin are skipped as well, also when their dependency failed later, for example in `Configure`. Plugins that depend on it only optionally keep running.

## Rules

- Dependencies are checked before any plugin code is loaded. Plugwright reads them from the assembly metadata without running the plugin.
- Plugins added in code with `Add<T>()` can depend on other plugins added in code, but not on plugins from a directory. They are part of the host application and are loaded before any plugin directory is read.
- Optional dependencies take part in the order and in cycle detection like required ones.

## See also

- [Plugin metadata](../reference/plugin-metadata.md)
- [Load contexts and shared assemblies](../explanation/load-contexts.md)
