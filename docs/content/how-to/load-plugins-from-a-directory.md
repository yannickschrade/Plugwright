# Load plugins from a directory

This guide shows how to build a plugin as its own project and load it from a plugin directory, without referencing it from the host application. The complete, runnable project is in `samples/DirectoryPlugins` in the repository.

!!! warning "Important"

    Plugins run with full trust inside your process. Whoever can write to the plugin directory can run code in your application. Protect the directory, and [verify plugins](verify-plugins-with-a-lock-file.md) before they are loaded. Without a verifier, Plugwright loads whatever is in the directory.

## Create the plugin project

A plugin is a class library with a type that implements [`IPlugin`](../api/Plugwright.IPlugin.md):

```csharp
--8<-- "samples/DirectoryPlugins/Acme.Greeter/GreeterPlugin.cs:plugin"
```

Two settings in the project file make the build output a deployable plugin folder:

```xml
--8<-- "samples/DirectoryPlugins/Acme.Greeter/Acme.Greeter.csproj"
```

- `EnableDynamicLoading` copies all dependencies of the plugin into its build output.
- `Private="false"` and `ExcludeAssets="runtime"` compile against the plugin contract without copying `Plugwright.Abstractions.dll`. The host provides it. When you reference the `Plugwright.Abstractions` package instead of the project, set `ExcludeAssets="runtime"` on the `PackageReference` in the same way.

## Lay out the plugin directory

Every plugin has its own subfolder. The folder contains the plugin's main assembly, named like the folder, together with its dependencies:

```text
plugins/
  Acme.Greeter/
    Acme.Greeter.dll
    Acme.Greeter.deps.json
    Microsoft.Extensions.Options.ConfigurationExtensions.dll
    ...
  Acme.Reports/
    Acme.Reports.dll
    ...
```

Publish the plugin project straight into its folder:

```bash
dotnet publish Acme.Greeter -c Release -o /srv/myapp/plugins/Acme.Greeter
```

The plugin ID is the name of the main assembly, here `Acme.Greeter`. Every public, non-abstract type in the main assembly that a registered handler can handle is a plugin. All of them share the plugin ID.

## Load the directory

Add the directory with `builder.Plugins.AddFromDirectory()`:

```csharp
--8<-- "samples/DirectoryPlugins/App/Program.cs:host"
```

A relative path, such as `AddFromDirectory("plugins")`, is resolved against the host's content root (`builder.Environment.ContentRootPath`), the same folder `appsettings.json` is read from. The sample uses the application's own folder instead, so it finds the plugins no matter which directory it is started from.

You can call `AddFromDirectory()` several times and combine it with `Add<T>()`. Plugins are processed in the order you add them; within a directory, in the alphabetical order of their folders. [Dependencies](declare-plugin-dependencies.md) come first.

## Configure the plugin

Plugin settings work the same way as for plugins added in code. The section uses the plugin ID:

```json
--8<-- "samples/DirectoryPlugins/App/appsettings.json"
```

## What can go wrong

| Situation | Behavior |
| --- | --- |
| The directory does not exist. | Warning with event ID 1002, no plugins are loaded from it. The application starts. |
| A DLL lies directly in the directory, outside a subfolder. | Warning with event ID 1003, the DLL is ignored. |
| A subfolder has no assembly with its name, the assembly cannot be loaded, or it contains no plugin type. | [`PluginException`](../api/Plugwright.PluginException.md), or the plugin is skipped with [`PluginFailureMode.SkipAndLog`](../api/Plugwright.PluginFailureMode.md). |
| Two sources provide the same plugin ID, for example the same plugin in two directories. | [`PluginException`](../api/Plugwright.PluginException.md) naming both sources, also with `SkipAndLog`. |

## See also

- [Load contexts and shared assemblies](../explanation/load-contexts.md)
- [Keep running when a plugin fails](handle-plugin-failures.md)
- [`PluginCollection.AddFromDirectory`](../api/Plugwright.PluginCollection.md#Plugwright_PluginCollection_AddFromDirectory_System_String_)
