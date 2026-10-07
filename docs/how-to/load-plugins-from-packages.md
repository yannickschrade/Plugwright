# Load plugins from NuGet packages

Plugins can be shipped as NuGet packages. The package carries the plugin's version, description and dependencies, and library packages bring the plugin's dependencies along. This guide shows how to build a plugin package and load it from a package folder.

The complete, runnable project is in `samples/PackagePlugins` in the repository.

## Add the package source

The package source is in its own package, so that the core of Plugwright has no NuGet dependencies:

```bash
dotnet add package Plugwright.NuGet
```

Then add the package folder before calling `ApplyPlugins()`:

[!code-csharp[](../../samples/PackagePlugins/App/Program.cs?name=host)]

A relative path is resolved against the content root. If the folder does not exist, a warning is logged and no plugins are loaded from it.

The sample also verifies every package against a lock file; see [Verify packages](#verify-packages).

## Build a plugin package

A plugin package is an ordinary NuGet package with the package type `PlugwrightPlugin`:

[!code-xml[](../../samples/PackagePlugins/Acme.Weather/Acme.Weather.csproj)]

- `<PackageType>PlugwrightPlugin</PackageType>` marks the package as a plugin. Packages without it are library packages.
- The package must contain an assembly named like the package ID for .NET 10 (or an earlier .NET version it is compatible with), here `lib/net10.0/Acme.Weather.dll`. Its public plugin types are the plugins.
- `<Version>` and `<Description>` become the plugin's metadata.

The plugin itself is the same as for any other source:

[!code-csharp[](../../samples/PackagePlugins/Acme.Weather/WeatherPlugin.cs?name=plugin)]

Create the package with `dotnet pack` and copy it, together with its library packages, into the package folder:

```bash
dotnet pack Acme.Weather -c Release -o /srv/myapp/packages
```

## Dependencies

Every dependency in the package's `.nuspec` must be satisfied by one of these:

| Dependency | Satisfied by |
| --- | --- |
| Another plugin package | The plugin package in the folder. It is a [plugin dependency](declare-plugin-dependencies.md): processed first, with shared types, and its version range must match. |
| A library package | A library package in the folder, in the lowest version that matches the range. Its own dependencies are resolved the same way. |
| A package the host already provides | The host. Examples: `Plugwright.Abstractions`, `Microsoft.Extensions.*` and packages that are part of .NET, such as `System.Text.Json`. |

A dependency that none of them satisfies makes the plugin fail, with a message that names the package and the version range.

All other packages in the folder are ignored unless a plugin package depends on them.

## Plugin metadata

The package metadata wins over the assembly:

| Property | Source |
| --- | --- |
| ID and version | `.nuspec` |
| Description, authors | `.nuspec`, otherwise the assembly and `[assembly: Plugin]` |
| Dependencies | `.nuspec` dependencies on plugin packages, with their version ranges, plus `[assembly: PluginDependency]` |

Configure the plugin under its package ID, `Plugwright:Plugins:Acme.Weather:Settings`.

## Verify packages

Packages are code that runs in your process, library packages as much as plugin packages. With verifiers registered, every package in the folder must pass them before anything is read from it. Two verifiers can verify packages:

- The <xref:Plugwright.HashVerifier> compares every package file with a lock file, as for plugin folders. Create the lock file for the package folder with `plugwright lock`; it lists every `.nupkg` under its file name:

  ```bash
  plugwright lock /srv/myapp/packages --output /etc/myapp/plugins.lock.json
  ```

- The <xref:Plugwright.PackageSignatureVerifier> checks the NuGet signature of every package. See [Verify package signatures](verify-package-signatures.md).

A library package that fails verification is not used, so plugins that depend on it fail as well.

## What happens at runtime

Plugwright reads every package once into memory and loads the plugin's assemblies from there; nothing is extracted. Native libraries are written to a temporary folder of the process. See [Load contexts and shared assemblies](../explanation/load-contexts.md#plugins-from-packages).

Because assemblies come from memory, <xref:System.Reflection.Assembly.Location> is empty for them.

## See also

- [Load plugins from a directory](load-plugins-from-a-directory.md)
- <xref:Plugwright.PluginPackageExtensions>
