# Develop plugins in a solution

While you develop an application and its plugins together, keep them in one solution and reference the plugins as projects. You get breakpoints, Hot Reload and refactoring across host and plugins. Loading plugins from a directory or a package can come later, without changing the plugins.

## Lay out the solution

For an application with an API host and a Blazor host, `samples/SplitHosts` shows a layout that scales:

```text
SplitHosts.AppHost/             .NET Aspire app host (optional)
SplitHosts.ServiceDefaults/     Aspire service defaults (optional)
SplitHosts.Api/                 API host: Plugwright.AspNetCore, backend plugins
SplitHosts.Web/                 Blazor host: Plugwright.AspNetCore, UI plugins
Acme.Orders/                    backend plugin (class library)
Acme.Orders.Ui/                 UI plugin (Razor class library)
Acme.Orders.Abstractions/       DTOs shared by both parts (class library, no plugin)
```

- Each host references its plugins as projects and adds them with `builder.Plugins.Add<T>()`.
- Backend plugins are class libraries, UI plugins Razor class libraries. Both reference `Plugwright.Abstractions`, and `Plugwright.AspNetCore.Abstractions` for web or Blazor contracts.
- What both parts of a feature share goes into an abstractions project of the feature. Contracts of the application itself, such as menu entries, go into an abstractions project of the application. See [Offer extension points to plugins](offer-extension-points.md).

See [Split a plugin across an API host and a UI host](split-plugin-across-hosts.md) for the code.

## Debug plugins

Plugins referenced as projects are part of the application: breakpoints, stepping and exceptions work as in your own code.

Plugins loaded from a directory without verifiers are loaded from their files, with their PDB files next to them, so breakpoints work as well. With verifiers, plugins are loaded from the verified bytes in memory, and a debugger does not find PDB files next to them. Embed the symbols in the plugin assembly with `<DebugType>embedded</DebugType>` in the plugin project, or debug without verifiers.

## Hot Reload

`dotnet watch` and Hot Reload in Visual Studio and Rider apply changes to referenced plugin projects, Razor components of Razor class libraries included:

```bash
dotnet watch --project SplitHosts.Web
```

Changes that Hot Reload cannot apply, such as new plugin classes or changes in `Configure`, need a restart, because plugins are configured once at startup. Plugins loaded from a directory or a package are not part of Hot Reload.

## Use Plugwright from source

To work on your application and on Plugwright at the same time, add the Plugwright repository as a Git submodule and reference its projects instead of the NuGet packages:

```bash
git submodule add https://github.com/yannickschrade/Plugwright external/Plugwright
```

```xml
<ItemGroup>
  <ProjectReference Include="..\external\Plugwright\src\Plugwright.AspNetCore\Plugwright.AspNetCore.csproj" />
</ItemGroup>
```

Plugwright's projects bring their own build settings and package versions from the submodule, so your solution needs no changes for them. To switch back to the packages, replace the project references with package references of the same names.

## See also

- [Add a plugin](add-a-plugin.md)
- [Load plugins from a directory](load-plugins-from-a-directory.md), when the plugins should be deployed separately
- [Test applications that use Plugwright](test-apps.md)
