# Changelog

All notable changes to this project are documented in this file.
The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Changed

- `PluginReport.SkippedPlugins` and `SkippedPlugin` are replaced by the plugin catalog.
- Services that a plugin type registered before it failed are removed again.

### Added

- Plugin contract: `IPlugin`, `PluginContext`, `PluginMetadata`.
- `builder.Plugins`, `builder.PluginHandlers`, `builder.ApplyPlugins()` and `app.UsePlugins()` for every `IHostApplicationBuilder` and `IHost`.
- Handler pipeline with `IPluginHandler` and the built-in handler for `IPlugin`.
- `PluginHandler<TPlugin>` base class for handlers that process one plugin interface or base class.
- `builder.Plugins.AddFromDirectory()`: loads plugins from `plugins/<Name>/<Name>.dll`, each into its own `AssemblyLoadContext`. `Plugwright` and `Microsoft.Extensions.*` are shared with the host.
- Duplicate plugin IDs from different sources throw a `PluginException`.
- `builder.Plugins` enumerates the added sources as `PluginTypeSource` and `PluginDirectorySource`.
- Plugin metadata: semantic version, description, authors and dependencies, read without running plugin code. `[assembly: Plugin]` and `[assembly: PluginDependency]` fill gaps.
- Optional plugin verification with `builder.PluginVerifiers`. `HashVerifier` checks plugin folders against a lock file; verified plugins are loaded from the verified bytes. Log events 1004–1006.
- `PluginReport` in the service container: active and skipped plugins and verification results.
- `CertificateVerifier` checks a signed manifest in each plugin folder; signers are trusted by thumbprint or as publisher (CA plus subject). Revocation checking is configurable.
- `plugwright` command-line tool (`Plugwright.Cli`) with the `lock` and `sign` commands.
- `Plugwright.NuGet`: `builder.Plugins.AddFromPackages()` loads plugin packages (package type `PlugwrightPlugin`) from a folder of `.nupkg` files in memory, with library packages and version ranges from the `.nuspec`.
- Package verification: the `HashVerifier` and `plugwright lock` cover `.nupkg` files; `PackageSignatureVerifier` checks NuGet package signatures.
- Optional feed for package folders: missing library packages are downloaded in the lowest matching version and stored in a download directory. Log event 1007 `PackageDownloaded`. `plugwright lock` accepts several directories.
- `builder.Plugins.AddFromConfiguration()` and `builder.PluginVerifiers.AddFromConfiguration()`: plugin sources, failure mode and verifiers from the `Plugwright` configuration section. `builder.Plugins.AddNuGetSupport()` adds package folders and the package signature verifier. `PluginPackageOptions.VerifyDownloads`.
- `Plugwright.AspNetCore` and `Plugwright.AspNetCore.Abstractions`: `IWebPlugin` and `WebPluginHandler` for MVC controllers, endpoints and middleware of plugins; `IBlazorPlugin`, `BlazorPluginHandler`, `IPluginComponentAssemblies` and `AddPluginAssemblies(app)` for Razor components of plugins. Works with `WebApplicationBuilder` and `WebApplication`, also together with .NET Aspire.
- `Plugwright.AspNetCore*` and `Microsoft.AspNetCore.*` are shared with plugins loaded from directories and packages.
- `pluginsettings.json`: application-managed settings file, added by `ApplyPlugins()` as the last configuration source (only keys under `Plugwright:Plugins`, reloads on change). `Options.SettingsFile`; `IPluginSettingsStore` saves settings and the on/off state.
- Plugin catalog: `IPluginCatalog`, `PluginInfo` and `PluginState` with the state of every plugin, also those not loaded. Log event 1008 `PluginStatus`, meter `Plugwright` with the gauge `plugwright.plugins`.
- Turn plugins off with `Plugwright:Plugins:{PluginId}:Enabled`. Disabled plugins are neither verified nor loaded; plugins that depend on them are `DependencyUnavailable`.
- Required plugins and a failure mode per plugin: `PluginPolicy`, `builder.Plugins.Add<T>(policy => …)`, `Configure(pluginId, …)` and `Require(pluginId)`. All problems of required plugins are reported in one exception.
- Startup tasks: `IPluginStartup` and `PluginContext.AddStartup<T>()` run after `Build()`, before every hosted service and the web server, in dependency order, each in its own scope. Log events 1009–1011, activities in the `ActivitySource` `Plugwright`.
- `MapPluginCatalog()` in `Plugwright.AspNetCore`: read-only endpoint with `PluginStatus` (in `Plugwright.Abstractions`).
- `builder.Plugins.AddFromAssembly(assembly)` and `AddFromReferencedAssemblies(prefix)`: plugin types of assemblies the application references, found through its `.deps.json` file. They count as plugins added in code.
- `IApplicationRestarter` (in `Plugwright.Abstractions`) restarts the application: with exit code 75 for a service manager (`ApplicationRestartMode.ExitCode`, default) or by starting a new process (`Relaunch`). `PluginOptions.RestartMode` and `RestartExitCode`; log events 1012 `ApplicationRestartRequested` and 1013 `RestartWaitTimedOut`.
- Optional plugin dependencies: `[PluginDependency(…, IsOptional = true)]`, `PluginContext.IsPluginLoaded(pluginId)` and `PluginInfo.OptionalDependencies`. `PluginInfo.Dependencies` lists only required dependencies.
- Health check for plugins: `builder.Services.AddHealthChecks().AddPlugins()` reports plugins that do not run because of an error (`Degraded` by default); plugins that are turned off are healthy.
- `builder.Plugins.ShareAssembly(assembly)` and `ShareAssemblies(prefix)`: plugins from directories and packages use the host's copy of these assemblies, for example application contracts or Entity Framework Core.
 checked before loading, applied in dependency order, with shared types across load contexts. Dependents of skipped plugins are skipped.
- Per-plugin settings section `Plugwright:Plugins:{PluginId}:Settings`.
- `PluginFailureMode.SkipAndLog` to skip failing plugins instead of stopping the application.
- Buffered logging in phase 1, log events `PluginLoaded` (1000), `PluginSkipped` (1001), `PluginDirectoryNotFound` (1002) and `AssemblyOutsidePluginFolder` (1003). Events name the plugin's source.
