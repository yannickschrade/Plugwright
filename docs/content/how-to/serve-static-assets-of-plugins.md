# Serve static assets of plugins

A Blazor plugin is a Razor class library. Its `wwwroot` files, the scoped CSS of its components and JavaScript modules next to them are static web assets. When the application references the plugin, the .NET SDK makes them part of the application at build time, and `app.MapStaticAssets()` serves them. Plugins loaded from a directory or a package at runtime need [`MapPluginStaticAssets()`](../api/Plugwright.PluginStaticAssetsEndpointRouteBuilderExtensions.md) and [`<PluginStyleSheets />`](../api/Plugwright.AspNetCore.PluginStyleSheets.md). The complete project is in `samples/BlazorPluginFolder` in the repository.

## Set up the host

Map the plugins' assets next to the application's own:

```csharp
--8<-- "samples/BlazorPluginFolder/App/Program.cs:host"
```

Link the plugins' scoped CSS in the `<head>` of `App.razor`, after the application's own bundle:

```razor
--8<-- "samples/BlazorPluginFolder/App/Components/App.razor:head"
```

`<PluginStyleSheets />` renders a `<link>` for each plugin from a directory or a package that has scoped CSS. The scoped CSS of referenced plugins is already part of the application's bundle and is not linked again.

## Use assets in the plugin

The plugin refers to its files as with a project reference, below `_content/{PluginId}/`, best through `@Assets`, which returns the fingerprinted route:

```razor
--8<-- "samples/BlazorPluginFolder/Acme.Weather.Ui/Pages/Weather.razor:page"
```

Scoped CSS (`Weather.razor.css`) and JavaScript modules next to a component (`Weather.razor.js`) work as usual; `<ImportMap />` contains the plugins' modules. Assets of component libraries that the plugin uses are served below their own `_content/{LibraryName}/` path.

`@Assets` and `<ImportMap />` know the plugins' assets because `.AddPluginAssemblies(app)` adds them to the application's own resource collection. The order of `MapPluginStaticAssets()` and `AddPluginAssemblies(app)` does not matter.

## Deploy the plugin

| Source | What to deploy |
| --- | --- |
| Directory | The output of `dotnet publish`. The output of `dotnet build` lacks the asset files; such a plugin loads, but its assets are skipped with the warning `PluginStaticAssetsNotPublished` (1014). |
| Package | The package that `dotnet pack` creates. Assets of library packages the plugin depends on are served as well. |

!!! warning "Do not reference the plugin from the application"

    A plugin loaded from a directory or a package must not be referenced by the application, not even with `ReferenceOutputAssembly="false"` to build it first. The SDK then adds the plugin's assets to the application's own, pointing to the plugin's source folder. The application serves them instead of the plugin: in `Development` from the source folder, otherwise with an empty response. Plugwright logs the warning `PluginStaticAssetsServedByApplication` (1016) in that case. To build and copy the plugin with the application, call its `Publish` target with the `MSBuild` task, as `samples/BlazorPluginFolder` does.

## How the assets are served

`MapPluginStaticAssets()` uses the endpoints that the SDK describes for every Razor class library, so plugin assets get what the application's own get: compression, ETags, cache headers and fingerprinted routes such as `_content/Acme.Weather.Ui/sun.k2h7f3x1qa.svg` with a cache time of one year.

- **Routes.** The plugin's own assets are below `_content/{PluginId}/`, assets of its libraries below `_content/{LibraryName}/`. The scoped CSS bundle of a published plugin is at the root, `{PluginId}.styles.css`, because it imports the bundles of its libraries with relative paths.
- **Routes the application already serves stay its own.** If the application and a plugin, or two plugins, use the same component library, the first one serves its assets; the others are skipped. The log event `PluginStaticAssetSkipped` (1015, debug) lists them.
- **The web root file provider changes.** `MapPluginStaticAssets()` replaces `IWebHostEnvironment.WebRootFileProvider` with one that also contains the plugins' assets, below `_plugwright/{Id}/`.
- **Packages are not compressed.** NuGet packages contain their assets uncompressed; published plugin folders contain Brotli and gzip versions. Compress the assets of packages at startup with `CompressPackageAssets`, or use [response compression](https://learn.microsoft.com/aspnet/core/performance/response-compression) in the application.

```csharp
app.MapPluginStaticAssets(options =>
{
    options.Source = PluginStaticAssetSource.Memory;
    options.CompressPackageAssets = true;
});
```

## Choose where the files come from

[`PluginStaticAssetsOptions.Source`](../api/Plugwright.AspNetCore.PluginStaticAssetsOptions.md#Plugwright_AspNetCore_PluginStaticAssetsOptions_Source) decides whether assets are served from disk or from memory:

| | `Disk` (default) | `Memory` |
| --- | --- | --- |
| Plugin folder without verifiers | From `wwwroot` of the plugin folder | From `wwwroot` of the plugin folder |
| Plugin folder with verifiers | From `wwwroot` of the plugin folder | From the verified bytes in memory |
| Package | From a temporary folder that the assets are written to at startup | From memory |
| Additional memory | None | The size of the packages' assets, with `CompressPackageAssets` plus their compressed versions |

`Memory` treats assets like the plugin's assemblies, which Plugwright also loads from the verified bytes.

### Security

With `Disk`, the files are read when they are requested, long after the plugin was [verified](verify-plugins-with-a-lock-file.md). Whoever can write to the plugin folder, or to the temporary folder of a package's assets, can change what the browser receives: a script, a style sheet or an image. Size, ETag and integrity hash come from the endpoint description read at startup, so a changed file of another size fails with an error, but a changed file of the same size is served. With verifiers registered, choose `Memory` to serve exactly the verified bytes, or protect the plugin folder and the temporary folder from writes by anyone but the deployment.

The temporary folder is `plugwright-assets/{process ID}-{random}` in the system's temporary folder. It is deleted when the process exits.

### Memory

With `Memory`, the assets of packages stay in memory as long as the application runs: about the size of the `staticwebassets` folders of the plugin packages and their library packages, and with `CompressPackageAssets` additionally their Brotli and gzip versions. Choose `Disk` if memory is scarce and the assets are large, for example images or fonts.

For plugin folders, `Memory` needs no additional memory: with verifiers registered, Plugwright keeps all verified files of a plugin folder in memory anyway, its `wwwroot` included, because it loads the plugin's assemblies from them. Without verifiers, plugin folders are served from disk in both modes.

## See also

- [Add Blazor pages from plugins](add-blazor-pages.md)
- [Security model](../explanation/security-model.md)
- [Static files in Blazor](https://learn.microsoft.com/aspnet/core/blazor/fundamentals/static-files)
