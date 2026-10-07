# CLAUDE.md — Plugwright

Plugwright is an open-source C# library that adds a plugin system to the .NET Generic Host (`IHostApplicationBuilder` / `IHost`). Plugins extend a host application through dependency injection. Excellent user documentation is a core requirement, not an afterthought.

The full specification (German, with rationale and decision log) lives in `docs/design/specification.md`. Read the relevant section before working on a feature. This file only holds the rules that always apply.

## Status

Done: repo setup (central package management, CI, DocFX), plugin contract, extension members and handler pipeline, directory loading, metadata and dependencies, verification infrastructure with `HashVerifier`, `PluginReport`, CLI `lock`. Next: `CertificateVerifier` with signed manifests (CLI `sign`), then NuGet source.

## Baseline

- Target `net10.0` only, C# 14 (extension members). No multi-targeting.
- License: MIT.
- Code, comments, XML docs, docs site, commit messages: English.
- Allowed dependencies: `Microsoft.Extensions.*`, `NuGet.Packaging` (only in `Plugwright.NuGet`), `System.Security.Cryptography.Pkcs`, `System.Reflection.MetadataLoadContext`. Ask before adding anything else.
- CI: GitHub Actions with build, tests and DocFX build on every push and PR.

## Intended repository layout

```
src/
  Plugwright.Abstractions/   Plugin contract (IPlugin, PluginContext, attributes)
  Plugwright/                Extension members, handler pipeline, loading, built-in verifiers
  Plugwright.NuGet/          NuGet folder source
  Plugwright.Cli/            dotnet tool: lock file, manifest signing, verify
tests/
  Plugwright.Tests/              Unit tests
  Plugwright.IntegrationTests/   Real loading, signing, verification
  TestPlugins/                   Test plugin projects (built and copied, never referenced)
samples/                     Runnable sample apps and plugins (source of all doc snippets)
docs/                        DocFX site; docs/design/specification.md
```

## Commands

- Build: `dotnet build`
- Test: `dotnet test`
- Docs: `dotnet tool restore`, then `dotnet docfx docs/docfx.json --warningsAsErrors` (add `--serve` to preview)

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
6. **Fail-fast by default.** An opt-in "skip and log" option (one global switch, name provisional, e.g. `builder.Plugins.Options.FailureMode`) covers errors in `Configure`, missing handlers and failed verification; skipping a plugin also skips everything that (transitively) depends on it.
7. **No hot reload, no unloading.** Enabling/disabling plugins (v2) takes effect after a process restart.

## Sources, identity, dependencies

- v1 sources: direct (`Plugins.Add<T>()`), DLL directory (one `AssemblyLoadContext` per plugin), NuGet folder (`.nupkg`, in `Plugwright.NuGet`). Source API names are provisional.
- DLL directory layout: `plugins/<Name>/<Name>.dll`. The folder is the unit for hashes, manifest and load context. DLLs directly in `plugins/` are ignored with a log warning.
  - Relative paths resolve against `ContentRootPath`. A missing directory logs a warning.
  - A folder without main assembly, with an unloadable assembly or without plugin types is a plugin failure.
  - Plugin types: public, non-abstract classes of the main assembly that a handler can handle.
- Duplicate plugin IDs (case-insensitive) always throw, naming both sources; skip-and-log does not apply.
- Load path: without verifiers `LoadFromAssemblyPath`; with verifiers `LoadFromStream` from the verified bytes (`Assembly.Location` is empty, document it). Native libraries are hash-verified, then loaded from path.
- Shared types (plugin contract, `Microsoft.Extensions.*`) always come from the default load context, in the host's version. Only if the host lacks such an assembly, the plugin's copy is loaded.
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
- No optional dependencies in v1.
- NuGet dependencies must be found in the folder, as plugin or library package.

## Configuration

- Per-plugin section: `Plugwright:Plugins:{PluginId}:Settings`. Sibling keys such as `Enabled` are reserved for the plugin system. Plugin classes in one assembly share the section.
- `pluginsettings.json` is app-managed:
  - Added by `ApplyPlugins()` as the last configuration provider, so it wins over everything, including environment variables.
  - `reloadOnChange` enabled; path configurable; saved via `IPluginSettingsStore`.
  - Its provider accepts only keys under `Plugwright:Plugins`.

## Integrity verification

- Optional. Verifiers are registered explicitly via `builder.PluginVerifiers`: built-in ones with `AddHashVerifier(...)` / `AddCertificateVerifier(...)` (delegate or explicit configuration section), custom ones with `Add(instance)` or `Add<T>()`. No verifiers → no verification, only a log notice (EventId 1004).
- All verifiers implement `IPluginVerifier` and declare supported sources via `CanVerify`. All responsible verifiers must pass. Verifiers registered but none responsible for a plugin → plugin failure. Plugins added in code are never verified.
- Verification failures of any kind are plugin failures (skip-and-log applies). Results: log events 1005/1006 and `PluginReport`.
- Hash mode: host-side lock file covering all plugin files; hashes carry an algorithm prefix (`sha256-…`).
- Certificate mode: NuGet package signatures; for DLL directories a CMS-signed manifest with plugin ID, version and hashes of all files. Files not listed in the manifest fail verification. Trust by certificate thumbprint or by issuer plus subject. No timestamps in v1 (certificate must be valid at verification time). Revocation mode configurable (`NoCheck`/`Online`/`Offline`), default `NoCheck`.
- CLI: package `Plugwright.Cli`, command `plugwright`. Commands: `lock` (done), `sign`, `verify`. No command-line parsing library.
- Verify before reading metadata. Load from the verified bytes (`LoadFromStream`). Re-verify or re-extract the NuGet cache on every start.
- Verifier settings come from code by default; binding them to a configuration section requires an explicit call. Which verifiers exist is always decided in code.
- Every check emits a structured log event with a fixed EventId; results are also available via API.
- No Authenticode or strong-name checks in the core.

## Code rules

- Nullable reference types enabled, warnings as errors.
- Full .NET naming conventions (dotnet/runtime style), enforced via `.editorconfig`: `_camelCase` private/internal instance fields, `s_camelCase` static fields, PascalCase constants, types and members, camelCase parameters and locals, no `this.`.
- Keep the public API small; everything else `internal`.
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

- DocFX only, English, structured by Diátaxis (tutorials, how-to guides, reference, explanation), hosted on GitHub Pages.
- Link API pages via `<xref:…>`; broken cross-references fail the build.
- Code snippets are included from `samples/` (by region), never pasted by hand.
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
  - NuGet folder: third-party package dependencies, extraction cache location and cleanup
- When a decision changes, add a row to the decision log in the specification.
