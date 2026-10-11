# CLAUDE.md — Plugwright

Plugwright is an open-source C# library that adds a plugin system to the .NET Generic Host (`IHostApplicationBuilder` / `IHost`). Plugins extend a host application through dependency injection. Excellent user documentation is a core requirement, not an afterthought.

The full specification (German, with rationale and decision log) lives in `docs/design/specification.md`. Read the relevant section before working on a feature. This file only holds the rules that always apply.

## Status

Done: repo setup (central package management, CI, docs with Zensical and DocFX API reference), plugin contract, extension members and handler pipeline, directory loading, metadata and dependencies, verification with `HashVerifier` and `CertificateVerifier`, `PluginReport`, CLI `lock` and `sign`, NuGet package source with verification (lock file, `PackageSignatureVerifier`) and optional feed, sources and verifiers from configuration (`AddFromConfiguration`), web hosts (`Plugwright.AspNetCore`, M1 of `docs/design/requirements-web-host.md`), `pluginsettings.json` with `IPluginSettingsStore`, plugin catalog, enabling/disabling, required plugins and per-plugin failure mode, catalog endpoint (M2). startup tasks (`IPluginStartup`, M3). Shared assemblies (R-52), assembly scan (R-53), health check (R-50), optional dependencies (R-15), `IApplicationRestarter` (R-32). Doc pages for M1–M3 are written. M4 questions decided (decision log 2026-10-11). Guides for R-14 and R-17 written. Static web assets of plugins (R-51: `MapPluginStaticAssets()`, `<PluginStyleSheets />`). M4 is complete. Next: CLI `verify` (step 7).

## Baseline

- Target `net10.0` only, C# 14 (extension members). No multi-targeting.
- License: MIT.
- Code, comments, XML docs, docs site, commit messages: English.
- Allowed dependencies: `Microsoft.Extensions.*` (core uses `DependencyModel` for the assembly scan and `Diagnostics.HealthChecks` for `AddPlugins()`), the ASP.NET Core shared framework (only in `Plugwright.AspNetCore*`), `NuGet.Packaging` and `NuGet.Protocol` (only in `Plugwright.NuGet`), `System.Security.Cryptography.Pkcs`, `System.Reflection.MetadataLoadContext`. Ask before adding anything else.
- CI: GitHub Actions with build, tests and docs build (DocFX API Markdown + Zensical, strict) on every push and PR.

## Intended repository layout

```
src/
  Plugwright.Abstractions/   Plugin contract (IPlugin, PluginContext, attributes)
  Plugwright/                Extension members, handler pipeline, loading, built-in verifiers
  Plugwright.NuGet/          NuGet folder source
  Plugwright.AspNetCore.Abstractions/  Web plugin contracts (IWebPlugin, IBlazorPlugin)
  Plugwright.AspNetCore/     Web and Blazor handlers, component assemblies
  Plugwright.Cli/            dotnet tool: lock file, manifest signing, verify
tests/
  Plugwright.Tests/              Unit tests
  Plugwright.IntegrationTests/   Real loading, signing, verification
  TestPlugins/                   Test plugin projects (built and copied, never referenced)
  TestWebHost/                   Web host for WebApplicationFactory tests
samples/                     Runnable sample apps and plugins (source of all doc snippets)
docs/                        zensical.toml, content/ (published pages), design/ (specification, not published), tools/
```

## Commands

- Build: `dotnet build`
- Test: `dotnet test`
- Docs: `dotnet tool restore`, `pip install -r docs/requirements.txt`, `dotnet docfx metadata docs/docfx.json --warningsAsErrors`, `dotnet run docs/tools/FixApiMarkdown.cs -- docs/content/api`, then in `docs/`: `zensical build --strict` (or `zensical serve`)

## Architecture rules — do not change without asking

1. **Extension members only.** No custom builder or app classes. Everything hangs off `IHostApplicationBuilder` and `IHost`, so console, worker and web hosts work the same way.
2. **Two explicit phases for every host type.** Never trigger them implicitly (no custom `IServiceProviderFactory` tricks).

   ```csharp
   var builder = Host.CreateApplicationBuilder(args);
   builder.Plugins.Add<MyPlugin>();
   builder.Plugins.AddFromDirectory("plugins");
   builder.PluginVerifiers.AddHashVerifier();      // optional

   builder.ApplyPlugins();   // phase 1, before Build()
   var app = builder.Build();
   app.UsePlugins();         // phase 2, after Build()
   await app.RunAsync();
   ```

3. **Guard misuse with clear exceptions:** `UsePlugins()` without `ApplyPlugins()`, `Plugins.Add…()` after `ApplyPlugins()`, and calling `ApplyPlugins()` or `UsePlugins()` twice.
4. **Plugins vs. handlers.** `builder.Plugins.Add…()` only records types and sources. Handlers decide what happens:
   - `IPluginHandler` with `CanHandle`, `Apply` (phase 1) and `Use` (phase 2).
   - Registered via `builder.PluginHandlers.Add<T>()`. The default handler for `IPlugin` is always registered.
   - Handlers are instantiated directly, not resolved from DI (the container does not exist yet).
   - All matching handlers run, in registration order, default handler first.
   - Handlers come only from the host app, never from plugin assemblies (v1).
   - When scanning a directory, a type counts as a plugin only if some handler can handle it. An explicitly added type without a handler throws.
   - The pipeline creates exactly one instance per plugin type and keeps it in `PluginDescriptor.Instance`; all handlers share it in both phases.
   - `IPluginHandler`, `PluginDescriptor` and `IPluginVerifier` live in `Plugwright`, not in Abstractions.
   - Phase 1 has no `ILoggerFactory`: log entries are buffered and flushed in `UsePlugins()`. Fail-fast exceptions must carry all relevant information themselves.
5. **Plugin contract:** `IPlugin.Configure(PluginContext context)`, parameterless constructor. `PluginContext` carries the builder, the plugin's own settings section, its metadata and a (buffered) `ILogger`. Extend `PluginContext` instead of changing `IPlugin`.
6. **Fail-fast by default.** `builder.Plugins.Options.FailureMode` (`FailFast`/`SkipAndLog`) is the global default; `PluginPolicy.FailureMode` overrides it per plugin (`Add<T>(policy => …)`, `Configure(id, …)`). It covers errors in `Configure`, missing handlers and failed verification; skipping a plugin also skips everything that (transitively) depends on it (`DependencyUnavailable`).
   - Required plugins (`PluginPolicy.IsRequired`, `Require(id)`, code only) always stop the start: missing, disabled, unavailable dependency, unverified or failing. One `PluginException` names all of them; problems known before phase 1 throw before any `Configure`.
   - Services a failing plugin type registered are taken back; configuration sources, properties and `ConfigureContainer` are not.
7. **No hot reload, no unloading.** Plugins are turned off with `Plugwright:Plugins:{Id}:Enabled` (v1); changes take effect after a restart. Disabled plugins are neither verified nor loaded; a dependency chain that starts at a disabled plugin is no failure.
8. **Plugin catalog.** `IPluginCatalog` (Abstractions) holds every known plugin with `PluginState` (`Loaded`, `Disabled`, `DependencyUnavailable`, `Failed`, `VerificationFailed`, `NoHandler`); log event 1008 per plugin, meter `Plugwright` with gauge `plugwright.plugins`. `PluginReport` keeps descriptors and verification results. `MapPluginCatalog()` (Plugwright.AspNetCore) exposes `PluginStatus` DTOs, error messages only on request.
9. **Startup tasks.** `PluginContext.AddStartup<T>()` with `IPluginStartup.StartAsync(CancellationToken)`; one internal `IHostedLifecycleService`, inserted first, runs them in `StartingAsync`: sequentially, dependency order, own scope each. Failure: required or fail-fast → host start fails; otherwise `Failed`, dependents `DependencyUnavailable` (their services stay registered). No stop hook.
10. **Static web assets of plugins** (Plugwright.AspNetCore): `app.MapPluginStaticAssets()` next to the host's unchanged `MapStaticAssets()`; own assets below `_content/{Id}/`, library assets keep their `_content/` route, a published plugin's scoped CSS bundle stays at the root; routes the host or an earlier plugin serves are skipped; `Source` `Disk` (default) or `Memory` (like assemblies); `CompressPackageAssets`; `<PluginStyleSheets />` links scoped CSS. Plugin folders need `dotnet publish` output. `@Assets`/`ImportMap` do not fingerprint plugin assets.

## Sources, identity, dependencies

- v1 sources: direct (`Plugins.Add<T>()`; `AddFromAssembly`, `AddFromReferencedAssemblies(prefix)` via `.deps.json` count as direct), DLL directory (one `AssemblyLoadContext` per plugin), NuGet folder (`.nupkg`, in `Plugwright.NuGet`). Source API names are provisional.
- DLL directory layout: `plugins/<Name>/<Name>.dll`. The folder is the unit for hashes, manifest and load context. DLLs directly in `plugins/` are ignored with a log warning.
  - Relative paths resolve against `ContentRootPath`. A missing directory logs a warning.
  - A folder without main assembly, with an unloadable assembly or without plugin types is a plugin failure.
  - Plugin types: public, non-abstract classes of the main assembly that a handler can handle.
- Duplicate plugin IDs (case-insensitive) always throw, naming both sources; skip-and-log does not apply.
- Load path: without verifiers `LoadFromAssemblyPath`; with verifiers `LoadFromStream` from the verified bytes (`Assembly.Location` is empty, document it). Native libraries are hash-verified, then loaded from path.
- Shared types (plugin contracts, `Microsoft.Extensions.*`, `Microsoft.AspNetCore.*`) always come from the default load context, in the host's version. Only if the host lacks such an assembly, the plugin's copy is loaded. The host adds its own with `Plugins.ShareAssembly(assembly)` / `ShareAssemblies(prefix)` (R-52; prefix matches at dots, global, code only).
- Identity, version and dependencies live at assembly/package level. Plugin classes in one assembly share them and load together.
- Metadata comes from `.nuspec` or the assembly. `[Plugin]` and `[PluginDependency]` attributes only fill gaps, never override. Conflicting version constraints must both be satisfied.
- Read metadata without executing plugin code (`MetadataLoadContext`).
- Version: SemVer from `<Version>` (informational version without `+…`). `[PluginDependency]` has `MinVersion` only. References to another plugin's assembly are dependencies and also check `AssemblyVersion` as a minimum.
- `[Plugin]` and `[PluginDependency]` are assembly-level only.
- Plugins added in code can only depend on plugins added in code.
- A dependency means presence + order + type sharing:
  - Missing dependency, version mismatch or cycle → exception that names the problem; with skip-and-log the affected plugins (all cycle members) and their dependents are skipped.
  - Dependencies are processed before dependents in both phases.
  - A plugin's load context forwards its dependencies' assemblies to their load contexts.
- Optional dependencies (R-15): `[PluginDependency(…, IsOptional = true)]`; present → like a required one; missing, disabled or failed → the plugin loads anyway; too old → error. `PluginContext.IsPluginLoaded(id)` in phase 1.
- NuGet folder (`AddFromPackages`, in `Plugwright.NuGet`, uses core internals via `InternalsVisibleTo`):
  - Plugin packages have package type `PlugwrightPlugin` and an assembly named like the package ID; all others are library packages.
  - Loaded in memory, no extraction; only native libraries go to a per-process temp folder, re-checked before loading.
  - Dependencies: plugin packages → plugin dependencies with version ranges; library packages from the folder (lowest matching version); packages the host provides need none; optional feed (`NuGet.Protocol`) downloads missing packages into a download directory (lowest matching), no network → plugin failure.
  - With verifiers, all packages are verified, libraries and downloads included. `HashVerifier`: lock entry per package file name; `PackageSignatureVerifier`: primary NuGet signature, same trust as `CertificateVerifier` (shared `CertificateTrust`), no timestamps.

## Configuration

- Per-plugin section: `Plugwright:Plugins:{PluginId}:Settings`. The sibling key `Enabled` turns a plugin off; other sibling keys are reserved for the plugin system. Plugin classes in one assembly share the section.
- `pluginsettings.json` is app-managed:
  - Added by `ApplyPlugins()` as the last configuration provider, so it wins over everything, including environment variables.
  - `reloadOnChange` enabled; path `Options.SettingsFile` (default `pluginsettings.json` in the content root, `null` turns it off); saved via `IPluginSettingsStore` (Abstractions; atomic writes). Moved back to the end after phase 1.
  - Its provider accepts only keys under `Plugwright:Plugins`.

## Integrity verification

- Optional. Verifiers are registered explicitly via `builder.PluginVerifiers`: built-in ones with `AddHashVerifier(...)` / `AddCertificateVerifier(...)` (delegate or explicit configuration section), custom ones with `Add(instance)` or `Add<T>()`. No verifiers → no verification, only a log notice (EventId 1004).
- All verifiers implement `IPluginVerifier` and declare supported sources via `CanVerify`. All responsible verifiers must pass. Verifiers registered but none responsible for a plugin → plugin failure. Plugins added in code are never verified.
- Verification failures of any kind are plugin failures (skip-and-log applies). Results: log events 1005/1006, `PluginReport` and the catalog (`VerificationFailed`).
- Hash mode: host-side lock file covering all plugin files; hashes carry an algorithm prefix (`sha256-…`).
- Certificate mode: NuGet package signatures; for DLL directories a CMS-signed manifest with plugin ID, version and hashes of all files. Files not listed in the manifest fail verification. Trust by certificate thumbprint or by issuer plus subject. No timestamps in v1 (certificate must be valid at verification time). Revocation mode configurable (`NoCheck`/`Online`/`Offline`), default `NoCheck`, applies to publishers only.
  - Manifest: `plugwright.manifest.p7s` in the plugin folder, CMS with embedded JSON (version, plugin ID, plugin version, hashes of all other files).
  - Publisher trust: root certificate file plus subject DN; intermediates come from the signature.
- CLI: package `Plugwright.Cli`, command `plugwright`. Commands: `lock` (several directories) and `sign` (done), `verify`. No command-line parsing library.
- Verify before reading metadata. Load from the verified bytes (`LoadFromStream`). Re-verify or re-extract the NuGet cache on every start.
- Verifier settings come from code by default; binding them to a configuration section requires an explicit call. Which verifiers exist is decided in code, unless the code explicitly hands that to configuration (`PluginVerifiers.AddFromConfiguration`).
- Every check emits a structured log event with a fixed EventId; results are also available via API.
- No Authenticode or strong-name checks in the core.

## Code rules

- Nullable reference types enabled, warnings as errors.
- Full .NET naming conventions (dotnet/runtime style), enforced via `.editorconfig`: `_camelCase` private/internal instance fields, `s_camelCase` static fields, PascalCase constants, types and members, camelCase parameters and locals, no `this.`.
- Keep the public API small; everything else `internal`.
- Code style follows Microsoft's conventions (dotnet/runtime), set in `.editorconfig` and enforced in the build: Allman braces, usings outside the namespace with `System` first, `var` for every local whose type can be inferred, `ConfigureAwait(false)` and argument checks in library code.
- One type per file, named after the type.
- Extension classes are named `{Feature}{ExtendedType}Extensions`, e.g. `PackagePluginCollectionExtensions`.
- Namespaces follow the package: `Plugwright` (core and `Plugwright.Abstractions`), `Plugwright.NuGet`, `Plugwright.AspNetCore` (also `Plugwright.AspNetCore.Abstractions`). Extension members of every package stay in `Plugwright`, so `using Plugwright;` finds them.
- Readability over cleverness: clear names, few abstraction layers.
- Every public type and member has XML doc comments, with examples where useful.

## Tests

- xUnit v3 (package `xunit.v3` 4.x) on Microsoft Testing Platform (`global.json` sets the test runner). Built-in `Assert`; Shouldly is allowed; never FluentAssertions.
- Unit tests in `Plugwright.Tests`, integration tests in `Plugwright.IntegrationTests`.
- Test plugins are separate projects under `tests/TestPlugins/`, built and copied like real plugins.
- An assembly fixture prepares test plugins, NuGet packages and signed manifests once per run.
- Each integration test uses its own temp directory.
- Tests touching load contexts run serially via xUnit collections.
- Restart and exit-code scenarios run in a separate process.

## Documentation

- Zensical site (`docs/zensical.toml`, pages in `docs/content/`), English, structured by Diátaxis (tutorials, how-to guides, reference, explanation), hosted on GitHub Pages. Navigation is the `nav` list in `zensical.toml`; add every new page there.
- The API reference comes from DocFX: `docfx metadata` writes Markdown to `docs/content/api/` (generated, not committed), `docs/tools/FixApiMarkdown.cs` turns its `<xref>` tags into links and writes `api/SUMMARY.md`, the API navigation tree (types grouped by kind, via Zensical's literate-nav). An unresolvable Plugwright reference in XML comments fails the build.
- Link API pages with relative links, e.g. ``[`PluginContext`](../api/Plugwright.PluginContext.md)`` or with a member anchor `#Plugwright_PluginContext_Configuration`. `zensical build --strict` fails on broken links, anchors and snippets.
- Admonitions use `!!! note` / `!!! warning "Important"`, not GitHub alerts.
- DocFX does not document C# 14 extension members (`builder.Plugins`, `ApplyPlugins()`, …); describe them on the containing class and in the guides.
- Code snippets are included from `samples/`, never pasted by hand: mark sections with `// --8<-- [start:name]` and `// --8<-- [end:name]`, include with `--8<-- "samples/…/File.cs:name"` inside a fenced block (paths from the repository root).
- No version switcher: use "Available since x.y" / "Changed in x.y" notes, a migration guide per major version and `CHANGELOG.md`.
- Docs must state clearly:
  - Getting Started shows verifier registration (the default is unverified).
  - Plugins run with full trust in the process.
  - `pluginsettings.json` overrides environment variables.
  - Lock files and `appsettings.json` with verifier settings must be protected from whoever can write to the plugin folder.

## Workflow

- Small steps: every feature ships with tests, docs and a sample.
- Order: plugin contract → extension members and handler pipeline → directory loading → metadata and dependencies → verification → NuGet source → CLI.
- Anything marked "Vorschlag" or "Offen" in the specification is not decided yet. Ask before implementing it. Currently open:
- When a decision changes, add a row to the decision log in the specification.
