# Changelog

All notable changes to this project are documented in this file.
The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- Plugin contract: `IPlugin`, `PluginContext`, `PluginMetadata`.
- `builder.Plugins`, `builder.PluginHandlers`, `builder.ApplyPlugins()` and `app.UsePlugins()` for every `IHostApplicationBuilder` and `IHost`.
- Handler pipeline with `IPluginHandler` and the built-in handler for `IPlugin`.
- Per-plugin settings section `Plugwright:Plugins:{PluginId}:Settings`.
- `PluginFailureMode.SkipAndLog` to skip failing plugins instead of stopping the application.
- Buffered logging in phase 1, log events `PluginLoaded` (1000) and `PluginSkipped` (1001).
