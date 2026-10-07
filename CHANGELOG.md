# Changelog

All notable changes to this project are documented in this file.
The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

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
- Dependencies between plugins: checked before loading, applied in dependency order, with shared types across load contexts. Dependents of skipped plugins are skipped.
- Per-plugin settings section `Plugwright:Plugins:{PluginId}:Settings`.
- `PluginFailureMode.SkipAndLog` to skip failing plugins instead of stopping the application.
- Buffered logging in phase 1, log events `PluginLoaded` (1000), `PluginSkipped` (1001), `PluginDirectoryNotFound` (1002) and `AssemblyOutsidePluginFolder` (1003). Events name the plugin's source.
