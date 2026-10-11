# Find plugins in referenced assemblies

When plugins are part of your application, as project or package references, you can add them without naming every plugin class. Plugwright searches the assemblies for plugin types when `builder.ApplyPlugins()` runs. The complete project is in `samples/ReferencedPlugins` in the repository.

## Reference the plugins

The application references its plugin projects like any library:

```xml
--8<-- "samples/ReferencedPlugins/App/ReferencedPlugins.App.csproj"
```

Each plugin project contains ordinary plugin classes:

```csharp
--8<-- "samples/ReferencedPlugins/Acme.Plugins.Orders/OrdersPlugin.cs:plugin"
```

## Search by name prefix

[`AddFromReferencedAssemblies`](../api/Plugwright.PluginCollection.md#Plugwright_PluginCollection_AddFromReferencedAssemblies_System_String_) searches all assemblies the application references whose names start with a prefix:

```csharp
--8<-- "samples/ReferencedPlugins/App/Program.cs:host"
```

- The prefix is compared without case and only at a dot: `Acme.Plugins` matches `Acme.Plugins` and `Acme.Plugins.Orders`, but not `Acme.PluginsLegacy`. A prefix is required, so that Plugwright does not search the whole framework and does not pick up plugin types from libraries you did not mean to add.
- The referenced assemblies come from the application's `.deps.json` file, which `dotnet build` and `dotnet publish` create. Assemblies that no code of the application uses are found as well. Without a `.deps.json` file, `builder.ApplyPlugins()` throws.
- A plugin type is every public, non-abstract class that a registered [plugin handler](write-a-plugin-handler.md) can handle, as for plugin directories. Matching assemblies without plugin types, such as contract libraries, are skipped.

## Add one assembly

To add the plugin types of one assembly, pass it to [`AddFromAssembly`](../api/Plugwright.PluginCollection.md#Plugwright_PluginCollection_AddFromAssembly_System_Reflection_Assembly_):

```csharp
builder.Plugins.AddFromAssembly(typeof(OrdersPlugin).Assembly);
```

Unlike the prefix search, an assembly added this way must contain plugin types. If it has none, that is a plugin failure.

## How these plugins behave

Plugins found this way count as plugins added in code:

- They run in the application's own load context and use its assemblies.
- They are never verified. Verifiers only check plugins from directories and packages.
- They can only depend on other plugins added in code.
- They can be [turned off](enable-and-disable-plugins.md) and [required](required-and-optional-plugins.md) by their plugin ID, the assembly name, like all plugins.

A type that you also add with `builder.Plugins.Add<T>()` is applied once.

!!! warning "Trimming"

    Trimming removes types that no code references, and plugin types found by searching are such types. In trimmed applications, add plugins with `builder.Plugins.Add<T>()`.

## See also

- [Add a plugin](add-a-plugin.md)
- [Load plugins from a directory](load-plugins-from-a-directory.md), for plugins that are not part of the application
