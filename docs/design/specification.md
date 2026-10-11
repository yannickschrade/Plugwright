# Plugwright – Specification of the Plugin System for the .NET Generic Host

Oct 6, 2026 · @Yannick

## Purpose and vision

The goal is a public C# library that provides a generic plugin system based on `IHost` and `IHostApplicationBuilder`. This document is its specification and later serves as the starting point for the implementation with Claude Code.

- Plugins extend a host application through dependency injection, e.g. with additional services or hosted services.
- The entry point is deliberately small: a console application with the .NET Generic Host.
- The library builds on Microsoft's standard abstractions instead of inventing its own host concepts.
- Excellent, thorough user documentation is a core requirement, not an afterthought.

Extension for web hosts, plugin state and startup: `requirements-web-host.md` in the same folder. This document is a work in progress and is extended over several sessions. Entries marked **Proposal** are not decided yet.

## Constraints

Fixed: C# class library, public on GitHub, built on `Microsoft.Extensions.Hosting`. Everything else is still a proposal.

| Topic | Status | Content |
| --- | --- | --- |
| Language | Fixed | C# |
| Project type | Fixed | Class library, distributed as NuGet package(s) |
| Publication | Fixed | Public GitHub repository |
| Foundation | Fixed | `IHost`, `IHostApplicationBuilder` from `Microsoft.Extensions.Hosting` |
| Target framework | Fixed | .NET 10 (LTS); enables C# 14 extension members |
| License | Fixed | MIT |
| CI/CD | Fixed | GitHub Actions from day one: build, tests and DocFX build (code samples are compiled too) on every push and PR; NuGet publishing follows later |
| Package split | Fixed | `*.Abstractions` only with the plugin contract (`IPlugin`, `PluginContext`, metadata, attributes, plugin catalog); main package with extensions, `IPluginHandler`, `PluginDescriptor`, `IPluginVerifier` and loading; NuGet source as its own package \*.NuGet; web and Blazor in `Plugwright.AspNetCore` with contracts in `Plugwright.AspNetCore.Abstractions` |
| Project name | Fixed | Plugwright; packages e.g. Plugwright, Plugwright.Abstractions, Plugwright.NuGet, Plugwright.Cli |

## Terms

These terms apply throughout the project, including code, docs and issues.

| Term | Meaning |
| --- | --- |
| Host application | The application that uses the plugin system and loads plugins |
| Plugin | A class that a handler processes; in the default case it implements `IPlugin` |
| Plugin assembly | A DLL that contains one or more plugins; carries their shared identity, version and dependencies |
| Plugin source | Where plugins come from: directly in Program.cs, DLL directory or NuGet folder |
| Handler | Component that knows what happens with a plugin type in phase 1 and phase 2 |
| `builder.Plugins` | Extension property; defines which plugins are loaded |
| Phase 1 (`ApplyPlugins`) | Before `Build()`: handlers register services, the container does not exist yet |
| Phase 2 (`UsePlugins`) | After `Build()`: the container exists, handlers work with the finished app |

## Architecture: extension members and two phases

**Decided:** The plugin system consists only of C# 14 extension members on `IHostApplicationBuilder` and `IHost`, without its own builder or app class. Every application, whether console, worker or web, uses its normal builder and calls the plugin system explicitly at two fixed points.

### Variants considered

| Variant | Idea | Advantages | Disadvantages |
| --- | --- | --- | --- |
| A: Wrapper classes | `PluginAppBuilder : IHostApplicationBuilder` wraps `HostApplicationBuilder`; `PluginApp : IHost` wraps the built host | Clear entry point, own properties like `Plugins` possible, familiar pattern from ASP.NET Core | Delegation code for all interface members; users with their own builder have to switch |
| B: Extension methods | `builder.AddPlugins(...)` on every `IHostApplicationBuilder` | Works with every builder (including web, worker) | No place for state; the plugin list has to travel via `Properties` or DI |
| C: C# 14 extension members | Extension property `builder.Plugins` on `IHostApplicationBuilder` | Looks like a real property, works with every builder | Needs C# 14 / .NET 10; state still lives in `Properties` |
| A + C combined | Wrapper as the default way, extension members for other builders | Serves both audiences | Two ways that must be documented and tested equally |

C was chosen. Wrapper classes (A) would only have had the advantage of controlling `Build()` themselves; that disappears with the explicit calls.

### Usage, the same for all host types

```csharp
var builder = Host.CreateApplicationBuilder(args); // or WebApplication.CreateBuilder(args)

builder.Plugins.Add<MyPlugin>();
builder.Plugins.AddFromDirectory("plugins");

builder.ApplyPlugins();   // Phase 1: before Build()

var app = builder.Build();
app.UsePlugins();         // Phase 2: after Build()

await app.RunAsync();
```

| Call | Extends | Task |
| --- | --- | --- |
| `builder.Plugins` | `IHostApplicationBuilder` (extension property) | Defines which plugins exist; only stores types and sources |
| `builder.ApplyPlugins()` | `IHostApplicationBuilder` | Phase 1: handlers register services; the plugin list is stored as a singleton in the DI container |
| `app.UsePlugins()` | `IHost` | Phase 2: handlers work with the built app, e.g. middleware or endpoints |

### Why explicit, even for the console

- `Build()` belongs to the respective builder; extension members cannot intercept the call.
- For web, the position of `UsePlugins()` determines the order in the middleware pipeline.
- One pattern for all host types, matching the .NET convention `AddX` / `UseX` / `MapX`.
- Rejected: automatic triggering via a custom `IServiceProviderFactory`. It collides with containers like Autofac and is hard for users to understand.

### Guarding against misuse

- `UsePlugins()` without a prior `ApplyPlugins()` throws a clear exception.
- `Plugins.Add…()` after `ApplyPlugins()` throws an exception.
- Calling `ApplyPlugins()` or `UsePlugins()` twice throws as well.

### Later for web

A split into `UsePlugins()` for middleware and `MapPlugins()` for endpoints, analogous to ASP.NET Core, is possible. For v1 one call is enough.

### What `IHostApplicationBuilder` already offers

`Services`, `Configuration`, `Environment`, `Logging`, `Metrics`, `Properties` and `ConfigureContainer`. Handlers and plugins use exactly these members instead of getting their own parallel paths.

## Handler pipeline

`Plugins.Add…()` only stores plugin types. **Decided:** Replaceable handlers decide what happens with a type in phase 1 and phase 2. New plugin kinds such as web, Blazor or custom ones are added as new handlers without changing the core.

```csharp
public interface IPluginHandler
{
    bool CanHandle(Type pluginType);

    // Phase 1, called by builder.ApplyPlugins()
    void Apply(PluginDescriptor plugin, IHostApplicationBuilder builder);

    // Phase 2, called by app.UsePlugins()
    void Use(PluginDescriptor plugin, IHost app);
}
```

| Handler | Package | Phase 1 (`ApplyPlugins`) | Phase 2 (`UsePlugins`) |
| --- | --- | --- | --- |
| Default | Core | Instantiates `IPlugin` and calls `Configure(context)` | – |
| Web | `*.AspNetCore`, later | Register services | Middleware and endpoints on `WebApplication` |
| Blazor | later | Register nav items as DI services | Make component assemblies known to routing |
| Custom | User | Free | Free |

### Rules

- Handlers are registered separately from plugins, directly on the builder: `builder.PluginHandlers.Add<T>()`. The default handler for `IPlugin` is always registered.
- Handlers run in phase 1 before the container exists and are therefore instantiated directly, not resolved from DI.
- All matching handlers process a plugin type, in registration order, the default handler first.
- When loading from a directory, only types that at least one handler can process count as plugins.
- An explicitly added type without a responsible handler leads to an exception pointing to the missing handler registration. The skip-and-log option applies here too.
- In v1, handlers come only from the host application, not from plugin assemblies. Otherwise a loaded plugin would decide how other plugins are treated.
- The pipeline creates exactly one instance per plugin type (parameterless constructor) and keeps it in `PluginDescriptor.Instance`. All handlers use this instance in both phases.
- `IPluginHandler` and `PluginDescriptor` live in the main package, not in Abstractions, since handlers only come from the host application.
- `PluginDescriptor.Instance` is of type `object`, since plugins do not have to implement `IPlugin`. For handlers that process a specific interface there is the base class `PluginHandler<TPlugin>`: it implements `CanHandle` and passes the instance typed.

### Logging in phase 1

In phase 1 there is no `ILoggerFactory` yet. **Decided:** Log entries from phase 1 (pipeline, verifiers, `PluginContext.Logger`) are buffered and written once the container exists, in `UsePlugins()`. If phase 1 aborts with fail-fast, the exception carries all necessary information itself.

## Plugin contract and lifecycle

**Decided:** A lean interface with exactly one required method that receives a context object in phase 1. Runtime behavior comes through normal DI means such as `IHostedService`, not through custom plugin hooks.

```csharp
public interface IPlugin
{
    void Configure(PluginContext context);
}

public sealed class HeartbeatPlugin : IPlugin
{
    public void Configure(PluginContext context)
    {
        context.Builder.Services
            .AddOptions<HeartbeatOptions>()
            .Bind(context.Configuration)   // own settings section
            .ValidateOnStart();

        context.Builder.Services.AddHostedService<HeartbeatService>();
    }
}
```

`PluginContext` contains the builder, the plugin's own configuration section, the plugin's metadata and an `ILogger` (buffered, see Logging in phase 1). New properties can be added later without breaking `IPlugin`. That is why this signature is fixed before 1.0.

### Flow

1. `builder.Plugins.Add…()` stores plugin types and sources.
2. `builder.ApplyPlugins()` finds the types and loads their assemblies if needed.
3. The pipeline instantiates each plugin exactly once (parameterless constructor, since the container does not exist yet). The responsible handlers run phase 1; the default handler calls `Configure(context)`.
4. `builder.Build()` creates the host and container.
5. `app.UsePlugins()` lets handlers run phase 2, if they have one.
6. Start and stop run through the normal host lifecycle of the registered hosted services.

### Open points

- Metadata and dependencies: see section Plugin metadata and dependencies.
- Behavior on errors in `Configure`: decided – fail-fast by default, skip-and-log via option.
- The option is a global switch, e.g. `builder.Plugins.Options.FailureMode = PluginFailureMode.SkipAndLog`. It applies to errors in `Configure`, missing handlers and failed verification. The exact name is provisional.

## Configuration per plugin

**Decided:** Every plugin has its own configuration section. Plugin settings live in a separate file `pluginsettings.json` that the app manages itself. `appsettings.json` stays the business of development and operations.

```json
{
  "Plugwright": {
    "Plugins": {
      "Acme.Heartbeat": {
        "Settings": { "Interval": "00:00:10" }
      }
    }
  }
}
```

### Rules

- Structure `Plugwright:Plugins:{PluginId}`. The plugin's own values live under `Settings`; next to it there is room for values of the plugin system such as `Enabled` in v2.
- `ApplyPlugins()` adds `pluginsettings.json` as a JSON configuration provider. The same section may also be in `appsettings.json`; both are merged.
- `pluginsettings.json` is added last and wins on conflicts, even over environment variables. The docs state this deviation from the usual order clearly.
- The provider for `pluginsettings.json` only accepts keys under `Plugwright:Plugins`. This way the file the app can write to can override neither other settings nor verifier settings.
- The path is configurable, the default is next to the app. The docs show writable locations, e.g. `ProgramData` on Windows or `/var/lib` on Linux.
- Saving happens via `IPluginSettingsStore`. The file counts as machine-managed; comments are lost on save.
- Changes are detected live (`reloadOnChange`). Plugins with `IOptionsMonitor` see new values without a restart. Only settings are reloaded, not plugins.
- Plugins read their section via `PluginContext.Configuration` and bind it with the options pattern.
- Several plugin classes of one assembly share their section, since the ID lives at assembly level. If needed they use subkeys.
- Plugin IDs with dots are awkward in Linux environment variables; the docs show alternatives.

## Plugin discovery and loading

**Decided:** v1 supports three plugin sources: directly in `Program.cs`, DLLs from a directory and NuGet packages from a folder. DLL and NuGet sources get an integrity check on load (hash, optionally certificate), see section Integrity verification. The API names are provisional.

| Source | Example API | Loading | Use |
| --- | --- | --- | --- |
| Direct | `Plugins.Add<MyPlugin>()` | No dynamic loading | Plugins in the same project or referenced as a package |
| DLL directory | `Plugins.AddFromDirectory("plugins")` | Own `AssemblyLoadContext` per plugin folder | Extension without recompiling |
| NuGet folder | `Plugins.AddFromPackages("packages")` | `.nupkg` is extracted, then loaded like a DLL | Ship plugins with version and package metadata |

### NuGet folder

- API: `builder.Plugins.AddFromPackages(path)` in `Plugwright.NuGet`.
- Plugin packages carry the package type `PlugwrightPlugin`; all other packages in the folder are library packages. A plugin package contains an assembly named like the package ID.
- No extraction: packages are read into memory once (and verified), assemblies are loaded from there. Only native libraries go to a temporary folder per process, are checked against the package content before loading and deleted on exit.
- Dependencies from the `.nuspec`: plugin packages become plugin dependencies with a version range; library packages come from the folder in the lowest matching version; whatever the host already has (e.g. `Microsoft.Extensions.*`, `Plugwright.Abstractions`, .NET packages) needs no package.
- Optionally a feed for missing packages (`NuGet.Protocol`, only in `Plugwright.NuGet`): `AddFromPackages(path, options => options.Feed = …)`, feed as a URL, local folder or name from NuGet.Config (with its credentials). Only library packages are downloaded, lowest matching version, stored in the download directory (default `plugin-package-downloads` in the content root). An unreachable feed, a missing version or a timeout (`DownloadTimeout`, default 30 s) is a plugin failure. Log event 1007 per download.
- `plugwright lock` accepts several directories for one lock file, e.g. the package folder and the download directory.
- With registered verifiers, all packages are verified, including library packages and downloads.
- `HashVerifier` verifies every source except plugins added in code. A package is listed in the lock file under its file name without `.nupkg`, with the `.nupkg` as its only file; `plugwright lock` picks up `.nupkg` files.
- `PackageSignatureVerifier` (in `Plugwright.NuGet`, `AddPackageSignatureVerifier`) verifies the primary NuGet signature (author or repository), the package's integrity and the signer with the same trust settings as the `CertificateVerifier`. Timestamps are not evaluated; the certificate must be valid at verification time.

### Layout of the DLL directory

**Decided:** Every plugin lives in its own subfolder, the main assembly is named after the folder: `plugins/<Name>/<Name>.dll`. The folder is the unit for hashes, manifest and load context. DLLs directly in `plugins/` are ignored, with a note in the log.

- A relative path resolves against `builder.Environment.ContentRootPath`, like `appsettings.json`.
- If the directory is missing, a warning is logged and no plugins are loaded from it.
- A subfolder without `<Name>.dll`, with an unloadable assembly or without a plugin type is a plugin failure (exception or skip and log).
- Plugin types are the public, non-abstract classes of the main assembly that a registered handler can process.
- Shared assemblies (`Plugwright*`, `Microsoft.Extensions.*`) come from the default context in the host's version, even if the plugin ships its own copies. If the host lacks such an assembly, the plugin's copy is loaded.
- Plugins are processed in the order of the sources, within a directory alphabetically by folder name.

### Load path

**Decided:**

- Without registered verifiers, assemblies are loaded with `LoadFromAssemblyPath`; `Assembly.Location` works.
- With verifiers, assemblies are loaded with `LoadFromStream` from the verified bytes; `Assembly.Location` is then empty. The docs mention this.
- Native libraries are hash-verified and loaded from their path right afterwards. The docs mention the small remaining time window.

### Important points for dynamic loading

- Shared types such as the plugin contract and `Microsoft.Extensions.*` must come from the default context. Otherwise types have the same names but are not compatible.
- A plugin's dependencies are resolved via `AssemblyDependencyResolver` and its `.deps.json`.
- Unloading plugins at runtime is not planned; enabling and disabling takes effect after a restart (see roadmap v2).
- Plugins run with full trust in the process. The docs must say so clearly.

## Plugin metadata and dependencies

**Decided:** Identity, version and dependencies live at the level of the assembly or NuGet package and come primarily from their own metadata. Code attributes are only the fallback for what package and assembly cannot express.

### Origin of the metadata

| Source | Identity and version | Description, author | Dependencies |
| --- | --- | --- | --- |
| NuGet folder | `.nuspec`: ID, version | `.nuspec` | `.nuspec` including version ranges, plus assembly references |
| DLL directory | Assembly name, assembly version | Assembly attributes from the `.csproj` | Assembly references |
| Direct in `Program.cs` | Assembly name, assembly version | Assembly attributes from the `.csproj` | Assembly references |
| Fallback, all sources | – | `[Plugin(...)]` | `[PluginDependency(...)]` for dependencies without an assembly reference |

Metadata and references are read without executing plugin code, via `MetadataLoadContext` (package `System.Reflection.MetadataLoadContext`, allowed as a dependency). Dependencies and integrity are thus checked before any code runs.

```csharp
// Only needed if Acme.Logging is not referenced directly or a minimum version should apply
[assembly: PluginDependency("Acme.Logging", MinVersion = "1.2.0")]
```

- `[Plugin]` and `[PluginDependency]` apply at assembly level only.
- The version is a semantic version from `<Version>` (`AssemblyInformationalVersion` without `+…`), compared according to SemVer 2.0. `[PluginDependency]` only knows `MinVersion`.
- A reference to another plugin's assembly is a dependency; in addition, its `AssemblyVersion` must be at least the referenced one.
- If the project sets neither `<Authors>` nor `<Company>`, the SDK fills in the assembly name; that counts as not set, and `[Plugin(Authors = …)]` then fills the gap.
- Plugins added in code can only depend on plugins added in code.

### Rules for dependencies

- A dependency means presence, order and type sharing.
- **Presence:** If a dependency is missing or its version does not match, an exception is thrown.
- **Order:** Dependencies are processed before the dependent plugin in phases 1 and 2. Cycles lead to an exception that names the cycle.
- **Type sharing:** A plugin's load context forwards assemblies of its dependencies to their load context so that types are compatible.
- NuGet dependencies must be found in the folder, either as a plugin package or as a library package.
- Several plugin classes in one assembly share identity and dependencies and are loaded together.
- Duplicate IDs, e.g. the same plugin in two directories or in code and in a directory, always lead to an exception that names both sources; skip-and-log does not apply here. IDs are compared case-insensitively, like configuration keys.
- With skip-and-log, a plugin is skipped together with all plugins that (transitively) depend on it, each with its own log entry.
- Optional dependencies only come in v2.

### Further decisions

- Attributes only add, they never override. If package and attribute name the same dependency with different versions, both conditions must be satisfied. For descriptive fields, the package or assembly wins.
- NuGet packages are read with `NuGet.Packaging` (version ranges, selection of the `lib/` folder, package signatures). The NuGet source lives in its own package `*.NuGet`, so the core stays free of NuGet dependencies.

## Integrity verification

**Decided:** Integrity verification is optional. The user registers verifiers explicitly, including the built-in hash and certificate verifiers. If no verifier is registered, no verification takes place; a notice appears in the log at startup.

```csharp
builder.PluginVerifiers.AddHashVerifier(options => options.LockFile = "/etc/myapp/plugins.lock.json");
builder.PluginVerifiers.AddCertificateVerifier(options => ...);
builder.PluginVerifiers.Add(new MyCompanyVerifier());   // custom verifier
```

| Mode | NuGet folder | DLL directory | Maintenance on updates |
| --- | --- | --- | --- |
| Hash | Package hash in a lock file on the host side | SHA-256 of all files of the plugin folder in the lock file | Regenerate the lock file on every update |
| Certificate | NuGet package signature, verified via `NuGet.Packaging` | Signed manifest per plugin folder: SHA-256 of all files, CMS-signed via `System.Security.Cryptography.Pkcs` | None, as long as the same publisher signs |

### Rules

- Trust comes from publisher certificates or the lock file. Both live on the host side, not in the plugin folder.
- Certificates are accepted by thumbprint or via a trusted issuer (e.g. a company CA) plus a restriction on the subject. This way a certificate renewal breaks nothing.
- The signed manifest contains plugin ID, version and all files. Files in the plugin folder that are not listed in the manifest cause a failure.
- All files of a plugin are verified, not only the main DLL.
- Assemblies are loaded from the verified bytes, e.g. with `LoadFromStream`, so nothing can be swapped between verification and loading.
- The extracted NuGet cache is checked against the hashes from the verified package on every start, or extracted again.
- Hashes carry an algorithm identifier, e.g. `sha256-…`, so the algorithm can change later.
- Verification runs before metadata is read via `MetadataLoadContext`.
- Failed verification leads to an exception; skip-and-log applies as for other load errors.
- All verifiers implement `IPluginVerifier`, built-in and custom ones alike. Each verifier states via `CanVerify` which sources it can verify. All responsible verifiers must pass. If verifiers are registered but none is responsible for a plugin, an exception is thrown. Verifier settings such as the lock file path and trusted certificates come from code by default. Optionally the developer binds them explicitly to a configuration section, e.g. `builder.PluginVerifiers.Add<CertificateVerifier>(builder.Configuration.GetSection("Plugwright:Verification:Certificate"))`. Which verifiers exist is always decided by the code.
- Every check emits a structured log event with a fixed EventId (plugin, version, mode, certificate, result). The results are also available via API.
- Strong-name signatures are not used; according to Microsoft they are not a security feature.

### Note for the docs

Verification only protects if the lock file and trust configuration cannot be changed by the same people who can change the plugin folder. If verifier settings live in `appsettings.json`, this applies to that file too.

### CLI tool

v1 contains a CLI tool as a `dotnet tool` with three tasks:

1. Generate a lock file for hash mode.
2. Sign the manifest of a plugin folder.
3. `verify`: check plugin folders against the verification configuration without starting the app, e.g. in CI or during deployment.

### Further decisions

- Built-in verifiers have their own registration methods (`AddHashVerifier`, `AddCertificateVerifier`) with settings via delegate or via an explicitly passed configuration section. Custom verifiers via `Add(instance)` or `Add<T>()`. One verifier per type.
- Plugins added in code are never verified; the rule "no responsible verifier → failure" only applies to plugin folders (and later NuGet).
- Verification failures, a missing responsible verifier, exceptions from a verifier and a missing or invalid lock file are plugin failures (exception or skip and log).
- Lock file: JSON with `version` and, per plugin folder (name compared case-insensitively), all files with `sha256-{base64}`. Default path `plugins.lock.json` in the content root.
- Results via API: `PluginReport` in the DI container with active and skipped plugins and all verification results.
- Certificates: no timestamps in v1; the certificate must be valid at verification time. Revocation check configurable (`NoCheck`, `Online`, `Offline`), default `NoCheck`.
- Signed manifest: one file `plugwright.manifest.p7s` in the plugin folder, CMS signature with embedded JSON (version, plugin ID, plugin version, hashes of all other files). The plugin ID must match the folder name; the version is bound via the hash of the main assembly.
- Trust by thumbprint (SHA-256 or SHA-1) or as a publisher: root certificate as a file plus subject (distinguished name, independent of notation). Intermediate certificates come from the signature. The revocation check applies to publishers only.
- `plugwright sign` reads the certificate from a PKCS#12 file; password via option or environment variable `PLUGWRIGHT_CERTIFICATE_PASSWORD`.
- CLI: package `Plugwright.Cli`, command `plugwright`. `lock` was brought forward so that docs and samples for verification use a real tool. Arguments are parsed without an additional library.

## Scope v1 and roadmap

**Proposal:** v1 delivers a console app with plugins from Program.cs, a DLL directory and a NuGet folder, fully documented.

### In v1

- [ ] `builder.Plugins`, `ApplyPlugins()` and `UsePlugins()` working, including the handler pipeline and default handler
- [ ] `IPlugin` contract with PluginContext in the Abstractions package; configuration per plugin via pluginsettings.json
- [ ] Sources: directly in Program.cs, DLL directory, NuGet folder, each with integrity verification and CLI tool
- [ ] Sample plugin that adds a hosted service
- [ ] Plugin metadata, dependencies between plugins and logging of the loaded plugins at startup
- [ ] Unit and integration tests
- [ ] User documentation and sample projects

### Not in v1

- Hot reload and unloading of plugins
- Host types other than ASP.NET Core and Blazor Server (see `requirements-web-host.md`), e.g. desktop

### Afterwards, in this order

1. Optional dependencies between plugins
2. Assembly scan as another source
3. Support for `WebApplicationBuilder` (ASP.NET Core)

### v2: Enabling and disabling plugins

**Proposal:** No hot reload. Plugins are enabled or disabled in the app; the change takes effect after a restart of the process.

1. The state (enabled/disabled) is stored persistently in pluginsettings.json.
2. `ApplyPlugins()` skips disabled plugins on the next start.
3. A service of the library triggers the restart cleanly, e.g. via `IHostApplicationLifetime.StopApplication()` with a defined exit code; Windows service or systemd restart the process.

Reasons against hot reload:

- The Microsoft DI container is immutable after `Build()`.
- Hosted services, middleware pipeline and Blazor components are set up once at startup.
- An unloadable `AssemblyLoadContext` is only freed without remaining references; leaks go unnoticed.

Pausing immediately stays the business of the individual plugin, e.g. via a feature flag through `IOptionsMonitor`.

## Documentation strategy

Documentation is written together with the code and counts as part of every finished feature. **Decided:** Structure according to the Diátaxis model, published as a static site on GitHub Pages.

| Area | Purpose | Examples |
| --- | --- | --- |
| Tutorials | Learning by doing | "First plugin in 10 minutes" |
| How-to guides | Solving concrete tasks | Hosted service via plugin, loading plugins from a directory |
| Reference | Complete API description | Generated from XML doc comments |
| Explanation | Understanding the background | Build vs. runtime phase, load contexts, design decisions |

### Rules

- Every public type and member has XML doc comments, including an example where useful.
- Every how-to guide points to a runnable project under `samples/`.
- Code samples in the docs are compiled in CI so they do not go stale.
- In the repo: `README.md`, `CONTRIBUTING.md`, `CHANGELOG.md`.

### Tool and language

**Decided:** The docs are in English and are built exclusively with DocFX, for guides and API reference alike. All content lives as Markdown in the repo.

- Guides link to API pages with `<xref:…>`. Broken cross-references fail the build (warnings as errors).
- References to .NET types are linked automatically via the Microsoft Learn xref data.
- Code samples are included from files under `samples/`, as `#region` if needed. This way the docs only show code that actually builds.
- The base is the modern DocFX template, customized via CSS variables. "Last updated" from Git is added with a small template customization.
- DocFX runs as a `dotnet tool`; the repo needs no Node.js toolchain.

### Versions

**Decided:** No version switcher for now.

- The docs always describe the current version.
- New or changed APIs carry notes such as "Available since 1.2" or "Changed in 2.0".
- Every major version gets a migration guide; plus `CHANGELOG.md`.
- Older states are reachable via the Git tags in the repo.
- A version switcher is only considered once two major versions are maintained in parallel.

## Guidelines for the implementation with Claude Code

The `CLAUDE.md` in the repository is later derived from this section. **Proposal** for the initial state:

### Repository structure

```
src/        Library and abstractions
tests/      Unit and integration tests
samples/    Runnable sample apps and plugins
docs/       User documentation
```

### Code rules

- Nullable reference types enabled, warnings as errors.
- Keep the public API small; everything else `internal`.
- Readability over cleverness: clear names, few abstraction layers.
- No dependencies other than `Microsoft.Extensions.*` without an explicit decision.

### Workflow

- Small steps: every feature with tests, docs and a sample as one package.
- First settle the plugin contract, then extensions and handler pipeline, then loading from a directory.
- Record deviations from this specification in the decision log below.

### Tests

**Decided:** xUnit v3 on the Microsoft Testing Platform, package `xunit.v3` in version 4.x (final since August 2026). Assertions with the built-in `Assert`, Shouldly if needed; no FluentAssertions (paid for commercial use since v8).

1. Separate test projects: `Plugwright.Tests` for unit tests, `Plugwright.IntegrationTests` for real loading, signing and verification.
2. Test plugins as separate projects under `tests/TestPlugins/`. They are not referenced but built and copied into the test folder, like real plugins.
3. Test plugins, NuGet packages and signed manifests are prepared once per run via an assembly fixture.
4. Every integration test works in its own temporary directory.
5. Tests that touch load contexts deliberately run serially via xUnit collections.
6. Scenarios with restart and exit code run in a separate process, e.g. via a small test host app.

## Open questions and decision log

### Open questions for the next sessions

- [x] Choose the architecture variant: A, C or A + C
- [x] Confirm the target framework: only .NET 10 or older versions too
- [x] Configuration per plugin
- [x] Project name and license
- [x] Docs tool and docs language
- [x] Test framework
- [x] Open points before starting the implementation (CI, package split, phase 1 logging, plugin instances, folder convention, load path and others)
- [x] Duplicate plugin IDs
- [x] Version semantics for DLL plugins
- [x] Directly added plugins with registered verifiers
- [x] NuGet folder: third-party package dependencies, extraction cache and cleanup
- [x] Certificates: timestamps, revocation check
- [x] CLI: command and package name
- [ ] Confirm scope of v1 and v2 (enabling/disabling)

### Decision log

| Date | Decision | Rationale |
| --- | --- | --- |
| 2026-10-06 | The foundation is `IHost` and `IHostApplicationBuilder` | Use Microsoft's modern standard abstractions |
| 2026-10-06 | Start with a console app on the Generic Host | Small, clear entry point |
| 2026-10-06 | Published as open source on GitHub | Hence the high bar for the docs |
| 2026-10-06 | The target framework is .NET 10 only | LTS, no legacy users, C# 14 extension members available; multi-targeting later if needed |
| 2026-10-06 | Errors in Configure: fail-fast by default, optionally skip and log | Silent skipping hides errors in production; option for scenarios that must not block |
| 2026-10-06 | Extension members only (variant C), no custom builder or app class | Works with every host type, including web; wrappers would have to be rebuilt per host type |
| 2026-10-06 | Two explicit calls for all apps: builder.ApplyPlugins() before, app.UsePlugins() after Build() | Uniform for console and web; the user controls the position in the middleware pipeline; .NET convention AddX/UseX |
| 2026-10-06 | Plugins.Add…() only stores types; the handler pipeline processes them | New plugin kinds (web, Blazor, custom) without changing the core |
| 2026-10-06 | Handlers are registered separately from plugins, directly on the builder | Clear separation: plugins describe what, handlers how |
| 2026-10-06 | All matching handlers run, in registration order | Handlers can be combined as building blocks per interface, no catch-all handlers needed |
| 2026-10-06 | No handler for a type: exception; the skip-and-log option applies too | Almost always a configuration error; consistent with fail-fast |
| 2026-10-06 | Handler registration via builder.PluginHandlers.Add\<T>(); in v1 only from the host application | "Handlers" has several meanings in .NET; handlers from plugins make the behavior unpredictable |
| 2026-10-06 | Term "plugin" instead of "module" | In .NET, "module" stands for splitting up one's own app; "plugin" for optional, loadable extensions; common search term |
| 2026-10-06 | v1 sources: directly in Program.cs, DLL directory, NuGet folder; DLL and NuGet with integrity verification | Requirement for v1 |
| 2026-10-06 | Plugin metadata and dependencies between plugins come in v1 | Requirement for v1 |
| 2026-10-06 | Metadata from .nuspec or assembly, code attributes as fallback; identity at assembly/package level | No duplicate maintenance; matches how plugins are shipped |
| 2026-10-06 | Dependency = presence, order, type sharing; optional dependencies only in v2 | Without type sharing, dependencies between isolated plugins would hardly be usable; optional ones need a contract concept |
| 2026-10-06 | NuGet.Packaging in its own package \*.NuGet | Do not rebuild version ranges, TFM selection and signatures; the core stays lean |
| 2026-10-06 | Attributes only add to package/assembly metadata, never override | Unambiguous rule, nothing is silently discarded |
| 2026-10-06 | Integrity verification in v1 by hash or certificate; DLLs via signed manifest, NuGet via package signature | Platform-independent; certificates save list maintenance on updates |
| 2026-10-06 | Without verification configuration: warning in Development, exception otherwise | Convenient in development, safe in production |
| 2026-10-06 | System.Security.Cryptography.Pkcs allowed as a dependency | Needed for CMS-signed manifests; Microsoft package |
| 2026-10-06 | Integrity extended: manifest binds ID/version, no foreign files; NuGet cache is verified; trust via issuer too; algorithm identifier | Closes tampering paths; certificate renewal and algorithm change without breaking |
| 2026-10-06 | IPluginVerifier, verification log with EventIds and API, CLI tool with verify | Extensible like handlers; traceable in production; checkable in CI |
| 2026-10-06 | Docs in English, exclusively with DocFX | Checked API cross-references, code samples from real code, .NET toolchain only; design flexibility deliberately secondary |
| 2026-10-06 | No version switcher for now; current docs with version notes and migration guides | More effort than benefit as long as no major versions are maintained in parallel |
| 2026-10-06 | Docs structured by Diátaxis, hosted on GitHub Pages | Clear separation by reader need; free hosting right next to the repo |
| 2026-10-06 | MIT license | Widespread in the .NET ecosystem, no hurdles for users |
| 2026-10-06 | Project name Plugwright | Easy to find, plugin reference in the name; ExtendR, Graft and Tenon are taken |
| 2026-10-06 | Test framework xUnit v3, separate unit and integration tests with their own test plugin projects | Assembly fixtures for expensive preparation, standard in .NET open source |
| 2026-10-06 | Verifiers are registered explicitly via builder.PluginVerifiers.Add\<T>(), including the built-in ones; without verifiers no verification, only a log notice. Replaces "Without verification configuration: warning in Development, exception otherwise" | The user decides about verification; uniform model as for handlers |
| 2026-10-06 | Several verifiers: all responsible ones must pass | Stricter and predictable; the weakest verifier cannot undermine anything |
| 2026-10-06 | Configuration per plugin in v1, in a separate pluginsettings.json under Plugwright:Plugins:{Id}; the file wins on conflicts | appsettings.json is part of the deployment and cannot be saved by the app; foundation for enabling in v2 |
| 2026-10-06 | IPlugin.Configure(PluginContext) instead of Configure(IHostApplicationBuilder) | A context object is extensible without breaking IPlugin after 1.0 |
| 2026-10-06 | Verifier settings in code, optionally bound explicitly to an appsettings section; pluginsettings.json may never set them | The settings are the basis of trust; the file the app can write to must not undermine them |
| 2026-10-07 | CI with GitHub Actions from the start: build, tests, DocFX build; NuGet publishing later | Code samples in the docs must be compiled in CI |
| 2026-10-07 | Package split confirmed; IPluginHandler, PluginDescriptor and IPluginVerifier in the main package | Handlers and verifiers only come from the host application; Abstractions stays minimal |
| 2026-10-07 | Calling ApplyPlugins() or UsePlugins() twice throws an exception | Almost always a mistake; consistent with fail-fast |
| 2026-10-07 | Log entries from phase 1 are buffered and written in UsePlugins(); with fail-fast the exception carries the information | There is no ILoggerFactory before Build(); a temporary factory would build providers twice |
| 2026-10-07 | PluginContext additionally contains a (buffered) ILogger | Plugins should be able to log directly while setting up |
| 2026-10-07 | Exactly one plugin instance per type, kept in PluginDescriptor.Instance, used by all handlers in both phases | No duplicate state; phase 2 needs access to the instance from phase 1 |
| 2026-10-07 | Plugin classes of one assembly share the settings section | Follows from identity at assembly level |
| 2026-10-07 | Skip-and-log as one global switch for Configure errors, missing handlers and failed verification | Small API; distinguishing by error kind later if needed |
| 2026-10-07 | Skipped plugins take all (transitively) dependent plugins with them | Dependents would otherwise fail on missing types |
| 2026-10-07 | DLL directory: plugins/<Name>/<Name>.dll, the folder is the unit for hashes, manifest and load context | Unambiguous main assembly without heuristics |
| 2026-10-07 | System.Reflection.MetadataLoadContext allowed as a dependency | Metadata without code execution with a manageable reflection API |
| 2026-10-07 | Load path: without verifiers LoadFromAssemblyPath, with verifiers LoadFromStream; native libraries from their path after hash verification | Compatible without verification, no swap between verifying and loading with verification; remaining risk for native libraries documented |
| 2026-10-07 | xUnit v3 in version 4.x (package xunit.v3 4.0.1) instead of 3.x | 4.x is final since August 2026; the specification planned the switch for then |
| 2026-10-07 | The same plugin or handler type added twice: ArgumentException | Almost always a mistake; does not concern the open question of duplicate plugin IDs |
| 2026-10-07 | Errors in IPluginHandler.Use (phase 2) always throw a PluginException, regardless of FailureMode | Skip-and-log is only defined for phase 1; after phase 1 a plugin cannot be removed cleanly |
| 2026-10-07 | Errors as PluginException with PluginId; misuse of the phases as InvalidOperationException | Can be caught specifically; standard exception for a wrong call order |
| 2026-10-07 | PluginDescriptor.Instance stays object; additionally a base class PluginHandler<TPlugin> with a typed instance | Plugins of a custom kind should not have to implement IPlugin; the base class saves casts and CanHandle in the most common case |
| 2026-10-07 | A relative path in AddFromDirectory resolves against ContentRootPath | Same base as appsettings.json; Generic Host convention |
| 2026-10-07 | Missing plugin directory: warning in the log, no exception | A fresh installation without plugins should start; typos are visible in the log |
| 2026-10-07 | Duplicate plugin IDs: exception naming both sources, even with SkipAndLog; case-insensitive comparison | Unclear which source should win; configuration keys are case-insensitive |
| 2026-10-07 | A broken plugin folder (missing or unloadable main assembly, no plugin type) is a plugin failure | A subfolder is a deliberate installation; its failure must not go unnoticed |
| 2026-10-07 | Shared assemblies come in the host's version; if the host lacks one, the plugin loads its copy | Type identity for the plugin contract and DI; plugins may use additional Microsoft.Extensions packages |
| 2026-10-07 | Plugin types in a directory: only public types of the main assembly | Clear rule without reflection on internal types |
| 2026-10-07 | PluginCollection is no longer enumerable; the plugin's source appears in error messages and log events (PluginSource) | With directories only the types from code would be enumerable; in production it must be clear where a plugin comes from |
| 2026-10-07 | PluginCollection enumerates the added sources (public: PluginSource, PluginTypeSource, PluginDirectorySource). Replaces "PluginCollection is no longer enumerable" | Iterable and honest: contains what was added. Directories are not scanned when added, since handlers, verifiers and failure mode are only known at ApplyPlugins() and unverified DLLs would otherwise be loaded. IPluginVerifier.CanVerify needs the source types later too |
| 2026-10-07 | The plugin version is SemVer from <Version> (InformationalVersion without build metadata); assembly references additionally check the AssemblyVersion as a minimum | Same format as NuGet packages later; the reference check follows the .NET binding rule |
| 2026-10-07 | [PluginDependency] with MinVersion only | Enough for DLL plugins; NuGet packages bring version ranges from the .nuspec; no custom range parser in the core |
| 2026-10-07 | [Plugin] and [PluginDependency] at assembly level only | Metadata and dependencies clearly belong to the assembly |
| 2026-10-07 | Missing or too old dependency, cycle: plugin failure; with SkipAndLog the affected plugins (for cycles all members) and their dependents are skipped | Consistent with fail-fast; other plugins should be able to keep running |
| 2026-10-07 | If one plugin type of an assembly fails, dependent plugins count as unsatisfied; other types of the same assembly keep running | Dependents may need exactly the services of the failed type; plugins in the host project should not block each other |
| 2026-10-07 | Plugins added in code cannot depend on plugins from a directory | They live in the default context and would load their own copy of the assembly; type identity would be impossible |
| 2026-10-07 | Metadata of plugin folders via MetadataLoadContext with the host assemblies (TPA) and the folder's files | Dependencies are checked before plugin code is loaded |
| 2026-10-07 | InternalsVisibleTo for Plugwright.Tests | Unit tests for SemVer comparison and dependency resolution without a public API for them |
| 2026-10-07 | Built-in verifiers via their own methods (AddHashVerifier, AddCertificateVerifier) with a delegate or configuration section; custom ones via Add(instance)/Add<T>() | Verifiers need settings; discoverable and type-checked, binding to configuration stays an explicit call |
| 2026-10-07 | Plugins added in code are not verified | They are part of the host application; otherwise every app would need a verifier for its own code |
| 2026-10-07 | No timestamps for manifest signatures in v1 | Much less effort; re-sign after the certificate expires |
| 2026-10-07 | Revocation check configurable, default NoCheck | Machines without network access should start; the docs recommend Online when trusting via issuer |
| 2026-10-07 | CLI Plugwright.Cli / plugwright; lock command brought forward before the remaining steps | Docs and samples for verification need a real tool for the lock file |
| 2026-10-07 | Verification failures of any kind are plugin failures and can be skipped with SkipAndLog | Consistent with the other load errors |
| 2026-10-07 | PluginReport in the DI container with active and skipped plugins and verification results | Fulfils "results via API" and the query for loaded plugins discussed earlier |
| 2026-10-07 | Notice "no verifiers registered" at level Information, only if plugin directories were added | Without directories there is nothing to verify; Information instead of Warning, since unverified loading is a legitimate choice |
| 2026-10-07 | Signed manifest as one file plugwright.manifest.p7s (CMS with embedded JSON) | One file instead of manifest plus signature; easy to sign and ship |
| 2026-10-07 | Trust as a publisher via a root certificate file and subject; intermediate certificates from the signature | Platform-independent without a certificate store; certificate renewal without changes on the host |
| 2026-10-07 | Revocation check only for publishers, not for thumbprints | Self-signed certificates have no revocation list; with thumbprints you remove the entry |
| 2026-10-07 | plugwright sign with a PKCS#12 file, password also from PLUGWRIGHT_CERTIFICATE_PASSWORD | Common format for signing certificates; the password need not end up in the shell history |
| 2026-10-07 | The SignedPlugins sample creates a sample CA at build time; no keys in the repository | Private keys do not belong in the repository; the docs snippet stays machine-independent |
| 2026-10-07 | Third-party package dependencies: from the folder, from the host or optionally from a feed; NuGet.Protocol allowed in Plugwright.NuGet | Convenient when wanted; without a feed no network access at runtime |
| 2026-10-07 | Packages are loaded in memory; only native libraries go to a temporary folder per process | No cache that has to be verified, protected and cleaned up |
| 2026-10-07 | Recognize plugin packages by the package type PlugwrightPlugin | Recognizable from metadata without loading code; library packages stay unambiguous |
| 2026-10-07 | Package signatures via a separate verifier in Plugwright.NuGet with the trust settings of the CertificateVerifier | The core stays free of NuGet dependencies |
| 2026-10-07 | Downloads from the feed go to a download directory, lowest matching version | Next start possible without network; reproducible and compatible with the lock file |
| 2026-10-07 | With verifiers, all packages are verified, including library packages | Libraries run as code in the process too |
| 2026-10-07 | Plugwright.NuGet uses internal types of the core (InternalsVisibleTo) instead of a public source API | The public API stays small; both packages are versioned together |
| 2026-10-07 | HashVerifier verifies all sources except code plugins; packages are listed in the lock file under their file name | One format for folders and packages; two versions of a library do not collide |
| 2026-10-07 | PackageSignatureVerifier verifies the primary signature and uses the trust logic of the CertificateVerifier (CertificateTrust) | Same trust rules for manifests and packages; the core stays without NuGet |
| 2026-10-07 | No evaluation of timestamps for package signatures either | Consistent with the manifests; for packages with a meanwhile expired certificate (e.g. older nuget.org packages) use the lock file |
| 2026-10-07 | The SignedPackages sample signs with a self-signed RSA certificate via the NuGet API | NuGet only accepts chains to a root the signing machine trusts, or self-signed certificates |
| 2026-10-07 | The feed only downloads library packages, no plugin packages | Plugins are installed deliberately; a feed must not sneak in new plugins |
| 2026-10-07 | Feed option DownloadTimeout, default 30 s | A missing network should not block the start for minutes |
| 2026-10-07 | Downloads are only stored after passing verification; log event 1007 per download | Rejected packages do not land on disk; network access at startup is visible |
| 2026-10-07 | plugwright lock with several directories | One lock file for the package folder and the download directory |
| 2026-10-07 | `Plugins.AddFromConfiguration` and `PluginVerifiers.AddFromConfiguration` read sources, failure mode and verifiers from the `Plugwright` section; only when called explicitly. Amends "Which verifiers exist is always decided by the code": the code can explicitly hand this decision to configuration | Operations should be able to change plugins and verification without recompiling; the call stays a deliberate decision in code, pluginsettings.json can never set these keys |
| 2026-10-07 | Unknown keys in these sections are errors; a verifier is on if its section has settings or is `true` | A typo must not silently turn verification off; empty JSON objects produce no configuration keys |
| 2026-10-07 | `PluginPackageOptions.VerifyDownloads`, default `true` | Whoever trusts their own feed need not add downloads to the lock file; safe as the default |
| 2026-10-10 | ASP.NET Core and Blazor Server, enabling/disabling via configuration, required plugins and an asynchronous startup hook come in v1 (`requirements-web-host.md`) | The target application with an API and a Blazor host needs them |
| 2026-10-10 | Packages `Plugwright.AspNetCore.Abstractions` (`IWebPlugin`, `IBlazorPlugin`) and `Plugwright.AspNetCore` (web and Blazor handlers, component assemblies, catalog endpoint); no separate `Plugwright.Blazor` | The contracts need ASP.NET Core types and do not belong in `Plugwright.Abstractions`; Blazor is part of the shared framework, a separate package saves no dependency |
| 2026-10-10 | `IPluginCatalog`, `PluginInfo`, `PluginState` and the endpoint DTO live in `Plugwright.Abstractions`; `PluginReport` stays for descriptors and verification results, `SkippedPlugins` is dropped | Plugins should be able to check their state without the main package; no two lists with the same content; not yet published |
| 2026-10-10 | Failure policy per plugin with the existing names `FailureMode`/`PluginFailureMode` instead of `FailurePolicy`; plus `Required` | No renaming without benefit |
| 2026-10-10 | `PluginSource` in the catalog as text instead of an enum | The name collides with the existing class `PluginSource` |
| 2026-10-10 | Web handler in phase 2 per plugin: first `ConfigurePipeline`, then `MapEndpoints` (instead of all `ConfigurePipeline` first) | `Map…` adds no middleware, the result is the same; otherwise `IPluginHandler` would need another hook |
| 2026-10-10 | Application parts in phase 1: extend an existing `ApplicationPartManager`, otherwise create one with the app's default parts that `AddControllers()` takes over later | Controllers are found whether `AddControllers()` comes before or after `ApplyPlugins()`, without forcing MVC |
| 2026-10-10 | `Plugwright.AspNetCore*` and `Microsoft.AspNetCore.*` are shared assemblies like `Microsoft.Extensions.*` | Type identity for web contracts with plugins from directories |
| 2026-10-10 | `IPluginStartup.StartAsync(CancellationToken)` without `IServiceProvider` | The task is resolved from DI per scope and gets services via its constructor, like `IHostedService` |
| 2026-10-10 | Disabled plugins from a directory or package: ID from the folder or package name, no verification, no loading; metadata only without verifiers | "Verify first, then read metadata" stays; a disabled, damaged plugin must not prevent the start |
| 2026-10-10 | Services of a plugin that failed in `Configure` are taken back (state of `builder.Services` before it); configuration sources, `Properties` and `ConfigureContainer` are not | The service list can be restored reliably, the other parts cannot |
| 2026-10-10 | `pluginsettings.json` is implemented before enabling/disabling | R-10 and R-13 require it |
| 2026-10-10 | Doc pages for the web requirements follow after the docs move from DocFX to Zensical; XML docs, tests and samples immediately | Do not write pages twice |
| 2026-10-10 | Samples may use Aspire and OpenTelemetry packages, libraries may not | Aspire ServiceDefaults as in the template |
| 2026-10-10 | Per-plugin settings in `PluginPolicy` (`Required`, `FailureMode`), set via `Add<T>(policy => …)`, `Configure(id, …)`, `Require(id)`; the global `FailureMode` is the default, the policy overrides it | One place for everything the code decides per plugin; works for all sources via the plugin ID |
| 2026-10-10 | Problems of required plugins known before phase 1 (missing, disabled, dependency unavailable, verification) throw before the first `Configure`; errors in `Configure` are collected until the end of phase 1 | R-11 demands resolution before phase 1; required failures in `Configure` can only be reported together afterwards |
| 2026-10-10 | A chain starting at a disabled plugin is no failure: dependent plugins become `DependencyUnavailable` without the failure mode applying | Disabling is a deliberate decision and should not abort the start |
| 2026-10-10 | Failed verification of a plugin package appears under the package ID, not under the file name | The catalog names plugins uniformly by ID |
| 2026-10-10 | Log event 1008 `PluginStatus` per plugin; meter `Plugwright` with gauge `plugwright.plugins` per state | R-12 and R-02; activities follow with the startup tasks (R-31) |
| 2026-10-10 | `MapPluginCatalog(pattern, includeErrorMessages)` as a simple parameter instead of an options class; DTO `PluginStatus` with states as text | Small API; readable JSON |
| 2026-10-10 | `IPluginSettingsStore` in `Plugwright.Abstractions` with `SetEnabledAsync` and `SaveSettingsAsync`; path via `Options.SettingsFile` or `Plugwright:SettingsFile` | Plugins can save their own settings; operations chooses a writable location |
| 2026-10-10 | Startup tasks: `PluginContext.AddStartup<T>()` registers the task as scoped plus an internal entry in the service list; an internal `IHostedLifecycleService` is inserted as the first hosted service and runs them in `StartingAsync` | Runs before every `StartAsync`, including the web server; the order from phase 1 is preserved; services taken back take their startup tasks with them |
| 2026-10-10 | Errors in a startup task follow the plugin's failure mode: required or `FailFast` → host start fails; `SkipAndLog` → `Failed`, dependents `DependencyUnavailable` | Same rules as in phase 1; "optional" from R-31 corresponds to `SkipAndLog` |
| 2026-10-10 | Log events 1009 `PluginStartupCompleted` (with duration), 1010 `PluginStartupFailed`, 1011 `PluginStartupSkipped`; activity `Plugwright.Startup` in the `ActivitySource` `Plugwright` | Traceable in production; activities are only recorded if a listener exists before the start |
| 2026-10-10 | No `Plugwright.Testing` package | Disabling and failure cases work with `WebApplicationFactory` and `UseSetting`; tests in `WebApplicationFactoryTests` show the pattern |
| 2026-10-10 | Docs site with Zensical instead of DocFX; DocFX only generates the API reference as Markdown (`docfx metadata`, `outputFormat: markdown`), a small tool (`docs/tools/FixApiMarkdown.cs`) resolves its `<xref>` references. Replaces "Docs exclusively with DocFX" | One site with a uniform look and search; Zensical checks links, anchors and code snippets in strict mode; API reference still from the XML comments |
| 2026-10-10 | Published pages in `docs/content/`, the specification stays in `docs/design/` | Zensical has no `exclude_docs`; the specification (German at the time) should not be published |
| 2026-10-10 | Code snippets via `pymdownx.snippets` with markers `// --8<-- [start:name]` instead of `#region` | Snippets knows no regions; missing files or sections fail the build |
| 2026-10-10 | Python (Zensical) as the docs tool, version in `docs/requirements.txt` | Zensical is a Python package; the rest of the toolchain stays .NET |
| 2026-10-10 | Known gap: DocFX does not document C# 14 extension members (dotnet/docfx#10943 skips them) | Until DocFX supports them, class docs and guides describe `builder.Plugins`, `ApplyPlugins()` etc. |
| 2026-10-10 | In tests with `WebApplicationFactory`, only `UseSetting` affects `ApplyPlugins()`; `ConfigureAppConfiguration` only applies at `Build()` and thus too late. Deviation from R-40, documented in "Test applications that use Plugwright" | Minimal hosting behavior: the factory's configuration callbacks run at `Build()` |
| 2026-10-10 | Namespaces by package: types of `Plugwright.NuGet` in `Plugwright.NuGet`, of `Plugwright.AspNetCore(.Abstractions)` in `Plugwright.AspNetCore`; `*.Abstractions` shares the namespace of its main package; extension members stay in `Plugwright` | Microsoft convention (e.g. `Microsoft.Extensions.Hosting.Abstractions`); extension members discoverable without an additional `using`; no break for users before the first release |
| 2026-10-10 | API navigation as a tree like on Microsoft Learn: `FixApiMarkdown.cs` generates `api/SUMMARY.md` (namespace, types grouped by kind), Zensical reads it via `literate-nav`; long names wrap at capital letters | All types reachable with one click; the navigation stays generated and needs no maintenance |
| 2026-10-11 | Code style according to Microsoft's conventions (C# conventions on Microsoft Learn, dotnet/runtime style), set in `.editorconfig` and enforced in the build; library code with `ConfigureAwait(false)` (CA2007) and argument checks (CA1062); `var` for all locals whose type the compiler can infer | Uniform code without discussion in review; `var` as a project decision, because dotnet/runtime only uses it when the type is obvious |
| 2026-10-11 | One type per file, the file is named after the type | Types can be found without searching, as in dotnet/runtime |
| 2026-10-11 | Extension classes are named `{Feature}{ExtendedType}Extensions`, e.g. `PackagePluginCollectionExtensions`, `PluginCatalogEndpointRouteBuilderExtensions` | Pattern of `Microsoft.Extensions.*` and ASP.NET Core |
| 2026-10-11 | `PluginPolicy.Required` is named `IsRequired` | Same as `PluginInfo.IsRequired`; prefix `Is` for Boolean properties per the Framework Design Guidelines |
| 2026-10-11 | R-52: `builder.Plugins.ShareAssembly(Assembly)` and `ShareAssemblies(prefix)`; prefix only at name boundaries (`X` matches `X` and `X.*`, not `XY`); applies globally to all plugins from directories and packages, code only; same rules as the built-in shared assemblies (host version wins; if the host lacks it, the plugin takes its copy) | Type identity for app contracts, UI libraries, EF Core; sharing per plugin would break type identity between plugins, only when there is a real need |
| 2026-10-11 | R-53: `builder.Plugins.AddFromReferencedAssemblies(prefix)` reads `deps.json` via `DependencyContext` (prefix required, name boundary as in R-52), plus `AddFromAssembly(Assembly)`; plugins found count as added in code; without `deps.json` an exception is thrown; not trimming-compatible (documented) | `AppDomain.GetAssemblies()` does not see references that are not loaded; without a prefix the framework would be scanned and foreign plugin types activated |
| 2026-10-11 | R-50: central health check in the core (`AddHealthChecks().AddPlugins()`, dependency `Microsoft.Extensions.Diagnostics.HealthChecks`) reports only plugins that do not run because of an error (`Failed`, `VerificationFailed`, `NoHandler`, `DependencyUnavailable` because of an error); disabled plugins and chains starting at them do not count; status on errors configurable, default `Degraded`; runtime checks are registered by plugins themselves in `Configure` | Plugin-owned checks disappear on their own with disabled or taken-back plugins; the central check only closes the gap of the failed ones; works for worker hosts too |
| 2026-10-11 | R-14: no `IPluginActivationSource`; a guide shows a custom configuration provider for `Plugwright:Plugins:{Id}:Enabled` | A configuration provider can already do that; no API without a concrete case |
| 2026-10-11 | R-15: optional dependencies via `[PluginDependency(…, IsOptional = true)]` (for packages only via the attribute, NuGet has no optional ones); present → like a required dependency (order, type sharing); missing, disabled or failed → the plugin loads anyway; a too old version is an error; the attribute downgrades a dependency derived from an assembly reference; query in phase 1 with `PluginContext.IsPluginLoaded(id)`, at runtime via `IPluginCatalog` | A wrong version is an installation error that should be visible; the plugin decides while registering its services |
| 2026-10-11 | R-32: `IApplicationRestarter.RequestRestart()` in `Plugwright.Abstractions`, always registered; `builder.Plugins.Options.RestartMode` = `ApplicationRestartMode.ExitCode` (default: set the exit code, `StopApplication()`) or `Relaunch` (restart the process with the same file and arguments; the new one waits for the old one to end, with a timeout); `RestartExitCode` default 75 (`EX_TEMPFAIL`); a custom registration replaces both; IIS restarts on the next request | How to restart depends on the app (service manager vs. console/desktop); a code ≠ 0 works with systemd `on-failure`, Windows services and Docker `on-failure`; a library does not start processes unasked |
| 2026-10-11 | R-51: the goal is full parity with project references: `_content/<Id>/…` from `wwwroot`, scoped CSS (bundle per plugin, paths via `IPluginComponentAssemblies`), `MapStaticAssets` with fingerprinting, compression, `@Assets`, `ImportMap`; delivery (plugin folder/temp vs. memory) and API only after a spike with a real RCL plugin | Plugins must not restrict Blazor features; unclear whether RCL publish produces an endpoint manifest and whether `MapStaticAssets` can handle several manifests and files without a physical path |
| 2026-10-11 | R-17: no mechanism in Plugwright; the guide "Split a plugin across hosts" shows the pattern from `SplitHosts` (the web host reads the API's catalog endpoint before `ApplyPlugins()`; unreachable → UI plugins stay on) and names the alternatives | Phase 1 has no DI and no service discovery and would have to check synchronously via HTTP; the start of both hosts would be coupled; the API's state changes after the UI has started |
| 2026-10-11 | Plugin = assembly or package stays the unit for catalog, health check, on/off, settings, required and dependencies; no dependencies or states per plugin class; the docs explain this ("One plugin, several classes") | Load context, metadata, verification and assembly references are per assembly; if only one class needs a dependency, an optional dependency with `IsPluginLoaded` helps; separate states mean separate plugins |
| 2026-10-11 | R-51 after the spike (`docs/design/spike-r51-static-assets.md`): map plugin assets separately from the host (`app.MapStaticAssets()` stays unchanged, plus `app.MapPluginStaticAssets()`); plugin manifests are rewritten to `_content/{Id}/`, `wwwroot` is attached to the `WebRootFileProvider` via a file provider | App code stays unchanged; price: `@Assets` and `ImportMap` do not know fingerprints of plugin files, but they are still served with compression, ETag and fingerprinted routes |
| 2026-10-11 | R-51: the source of the files is configurable: like assemblies (packages from memory, directories with verifiers from memory, without from disk) or from disk (packages via the temp folder); compression of package assets configurable: uncompressed or compressed in memory at startup | Little RAM should not force serving from memory; startup time can be weighed against bandwidth. Names and defaults of the options, and whether directory plugins in disk mode are served directly from their folder, to be settled during implementation |
| 2026-10-11 | R-51: scoped CSS via a component `<PluginStyleSheets />` in `Plugwright.AspNetCore` for the `<head>`; one `<link>` per plugin from a directory or package, not for plugins via project reference (already in `{App}.styles.css`) | No code per app; the component knows the fingerprinted route from the plugin's manifest |
| 2026-10-11 | R-51: directory plugins with static assets need publish output; packages provide their endpoints as MSBuild XML (`build/Microsoft.AspNetCore.StaticWebAssetEndpoints.props`), which Plugwright reads | Build output does not contain the files; packages contain no JSON manifest |
| 2026-10-11 | R-51 implemented: `PluginStaticAssetsOptions.Source` with `Disk` (default) and `Memory` ("like assemblies"), `CompressPackageAssets` (default `false`); directory plugins in disk mode directly from their `wwwroot`, packages via the temp folder `plugwright-assets`; the scoped CSS bundle of a published plugin stays at the root path (`{Id}.styles.css`), because it imports the bundles of its libraries relatively; routes the host or an earlier plugin already serves are skipped; build output is skipped with warning 1014 | Disk as the default saves memory (the user's decision); the docs name the security risk (files can change after verification; the same size is served) and the memory cost of `Memory` (package assets; directories with verifiers are in memory anyway) |
| 2026-10-11 | R-51 amended: `.AddPluginAssemblies(app)` adds the plugins' assets (label and integrity, like Blazor) to the host's `ResourceAssetCollection` in a `Finally` callback and sets a new `ImportMapDefinition`; this way `@Assets` and `ImportMap` provide fingerprints for plugin files too. Replaces the limitation from "map plugin assets separately from the host"; `app.MapStaticAssets()` stays unchanged | Spike: Blazor takes the most recently added collection, in `Finally` the host's is already present; no change to app code, order of calls irrelevant |
| 2026-10-11 | Warning 1016 when the app itself serves routes under `_content/{PluginId}/` of a plugin from a directory or package; asset tests also run in `Production` | Found during manual testing: a `ProjectReference` with `ReferenceOutputAssembly="false"` pulls the plugin assets into the app's build manifest, which only `Development` can resolve; `Production` returned empty responses. Until then tests only ran in `Development` |
| 2026-10-11 | All design documents in `docs/design/` translated to English; every document in the repository is written in English | Open source project; contributors must be able to read the specification and the decision log |
