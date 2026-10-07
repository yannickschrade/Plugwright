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

A dependency that none of them satisfies makes the plugin fail, with a message that names the package and the version range, unless a feed is configured.

All other packages in the folder are ignored unless a plugin package depends on them.

## Download missing packages

Optionally, missing library packages can be downloaded from a NuGet feed. The complete, runnable project is in `samples/PackageFeed` in the repository:

[!code-csharp[](../../samples/PackageFeed/App/Program.cs?name=host)]

- `Feed` is a URL such as `https://api.nuget.org/v3/index.json`, a local folder, or the name of a package source in NuGet.Config. Credentials for private feeds come from NuGet.Config.
- Only library packages are downloaded, in the lowest version that matches the dependency. Plugin packages are never downloaded; install them in the package folder.
- Downloads are stored in `DownloadDirectory` (default `plugin-package-downloads` in the content root). The next start finds them there and needs no network.
- Each download is logged with event ID 1007.

If the feed cannot be reached, has no matching version, or the download takes longer than `DownloadTimeout` (default 30 seconds), the plugin fails: by default the application stops, with [`SkipAndLog`](handle-plugin-failures.md) the plugin is skipped.

Downloaded packages are verified like all others before they are stored or used. With the lock file, create it after the first download, over both folders:

```bash
plugwright lock /srv/myapp/packages /srv/myapp/plugin-package-downloads --output /etc/myapp/plugins.lock.json
```

> [!IMPORTANT]
> A feed means network access during startup and code that was not in your deployment. Protect the download directory like the package folder, and verify packages if you use a feed.

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
