# Spike R-51: Static Web Assets of Dynamically Loaded Blazor Plugins

As of 2026-10-11. Goal according to the decision log: full parity with project references (`MapStaticAssets`, fingerprinting, compression, `@Assets`, `ImportMap`, scoped CSS). Tested with a Razor class library (`wwwroot/plugin.css`, `wwwroot/logo.svg`, `Hello.razor.css`, `Hello.razor.js`) and a Blazor host (.NET 10, `dotnet new blazor --interactivity Server`), plugin not referenced.

## What the plugin formats contain

| Format | Endpoints | Files | Compression | Scoped CSS |
| --- | --- | --- | --- | --- |
| `dotnet build` | `{Id}.staticwebassets.endpoints.json` (build) | **not** in the output, only via `staticwebassets.runtime.json` in the source folder | no | only in `obj/` |
| `dotnet publish` | `{Id}.staticwebassets.endpoints.json` (publish), routes **without** `_content/{Id}/` | `wwwroot/` | `.br` and `.gz` | `wwwroot/{Id}.styles.css` |
| `dotnet pack` | MSBuild items in `build/Microsoft.AspNetCore.StaticWebAssetEndpoints.props`, routes **with** `_content/{Id}/` | `staticwebassets/` | no | `staticwebassets/{Id}.{fingerprint}.bundle.scp.css` |

All formats with endpoints contain fingerprints, SRI hashes, ETags and cache headers.

## What works

1. **`MapStaticAssets(manifestPath)` reads the files via `IWebHostEnvironment.WebRootFileProvider`**, not relative to the manifest. Without a matching file provider, a mapped route returns 200 with empty content.
2. **Rewriting + file provider**: prefix the route, `AssetFile` and the `label` property with `_content/{Id}/`, attach the plugin's `wwwroot` via a prefix file provider into a `CompositeFileProvider` in front of `WebRootFileProvider`. Result: content, `Content-Encoding: br`, ETag, fingerprinted route with `max-age=31536000, immutable`, scoped CSS and JS module are served correctly.
3. **From memory** (custom `IFileProvider`, `PhysicalPath = null`): everything as with files from disk, including `Range` (206) and `If-None-Match` (304).
4. **`@Assets` and `ImportMap`**: `WithStaticAssets(pluginManifest)` **replaces** the host's collection (host files lose their fingerprints). Several manifests are not merged. What works: **one merged manifest** (host + all plugins), `MapStaticAssets(merged)` and `WithStaticAssets(merged)` once. Then host and plugin files have fingerprints, `ImportMap` contains the plugins' JS modules, `_framework/blazor.web.js` keeps working.
5. **Development** (host from `bin/Debug`, the host's build manifest): the merged manifest works as well.
6. Without `WithStaticAssets`, `@Assets["_content/{Id}/x.css"]` returns the path without a fingerprint; the file is served anyway (ETag, `must-revalidate`), just without long-term caching.

## What to keep in mind

- The host's `app.MapStaticAssets()` and a merged manifest must not both be mapped (duplicate routes). For full parity, the app has to replace its `MapStaticAssets()` call.
- `MapStaticAssets` only takes a file path: the merged manifest has to exist as a file (e.g. in the process's temp folder, like native libraries).
- Directory plugins need **publish output**; build output does not contain the files.
- Packages: read endpoints from MSBuild XML and translate them to JSON; no precompressed variants.
- Scoped CSS: plugins from directories/packages are not included in `{App}.styles.css`; their bundle has to be linked in the layout with a `<link>`. With project references it is already included and must not be linked twice.
- Integrity: manifest and `wwwroot` are part of the plugin files and thus covered by the lock file or manifest. Served from memory, the verified bytes apply; from disk, files could be swapped after verification.

## Addendum: `@Assets` without a merged manifest

The `ResourceAssetCollection` and the `ImportMapDefinition` are attached as metadata to the Razor components endpoints; Blazor takes the most recently added one. In a `Finally` callback of the `RazorComponentsEndpointConventionBuilder`, the host's collection is already present (tested: 30 entries). A new collection made of host entries plus plugin entries (per endpoint without selectors, only `label` and `integrity`, the way Blazor builds them) and an `ImportMapDefinition` created from it provide fingerprints for host and plugin files and an import map with the modules of both.
