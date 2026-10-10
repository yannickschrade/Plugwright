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

The attribute applies to the whole assembly, because all plugin classes in one assembly share their dependencies.

## When a dependency is not satisfied

| Situation | Message |
| --- | --- |
| The other plugin is missing. | `It depends on plugin 'Acme.Formatting', which is not available.` |
| The other plugin is too old. | `It requires plugin 'Acme.Formatting' version 1.2.0 or later, but version 1.1.0 is available.` |
| The plugin was compiled against a newer assembly version. | `It was compiled against assembly version 2.0.0.0 of plugin 'Acme.Formatting', but assembly version 1.0.0.0 is available.` |
| Plugins depend on each other in a circle. | `Plugins depend on each other in a cycle: Acme.A -> Acme.B -> Acme.A.` |

By default, each of these stops the application with a [`PluginException`](../api/Plugwright.PluginException.md). With [`PluginFailureMode.SkipAndLog`](../api/Plugwright.PluginFailureMode.md), the affected plugins are skipped instead, every plugin in a cycle included. Plugins that depend on a skipped plugin are skipped as well, also when their dependency failed later, for example in `Configure`.

## Rules

- Dependencies are checked before any plugin code is loaded. Plugwright reads them from the assembly metadata without running the plugin.
- Plugins added in code with `Add<T>()` can depend on other plugins added in code, but not on plugins from a directory. They are part of the host application and are loaded before any plugin directory is read.
- A dependency is always required. Optional dependencies are planned for a later version.

## See also

- [Plugin metadata](../reference/plugin-metadata.md)
- [Load contexts and shared assemblies](../explanation/load-contexts.md)
