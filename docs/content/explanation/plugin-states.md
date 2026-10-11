# Plugin states

Every plugin the application finds ends up in one [state](../api/Plugwright.PluginState.md), visible in the [catalog](../how-to/inspect-plugins-at-runtime.md). This page explains how a plugin gets there and why Plugwright decides the way it does.

## From source to state

`builder.ApplyPlugins()` works in steps, and each step can end a plugin's way:

1. **Find.** Every source lists its plugins: types added in code, folders of a plugin directory, plugin packages. A plugin that is [turned off](../how-to/enable-and-disable-plugins.md) stops here as `Disabled`, before anything of it is verified or loaded.
2. **Verify.** Plugins from directories and packages pass the registered verifiers, or end as `VerificationFailed`.
3. **Read and resolve.** Plugwright reads each plugin's metadata without running its code and resolves the dependencies. A plugin that cannot be read or is part of a cycle ends as `Failed`; a missing or too old dependency makes it `DependencyUnavailable`.
4. **Propagate.** Plugins that depend, directly or transitively, on a plugin that will not load become `DependencyUnavailable`. This happens before any plugin runs, so no plugin is configured in vain.
5. **Configure.** In dependency order, Plugwright loads each plugin, creates its classes and lets the handlers apply them. A class without handler ends as `NoHandler`, an exception as `Failed`.
6. **Start.** After `Build()`, when the host starts, the [startup tasks](../how-to/run-startup-tasks.md) run. A failing task makes its plugin `Failed` and its dependents `DependencyUnavailable`.

A plugin that passes all steps is `Loaded`.

## One plugin, several classes

A plugin is an assembly or a package with its plugin ID. Its plugin classes are entry points of that one plugin, not plugins of their own. Everything that describes or controls a plugin therefore works per plugin ID: its state in the catalog, the [health check](../how-to/report-plugin-health.md), [turning it on and off](../how-to/enable-and-disable-plugins.md), its settings, whether it is required, its dependencies and its verification. Only the log event `PluginLoaded` (1000) and [`PluginReport.Plugins`](../api/Plugwright.PluginReport.md#Plugwright_PluginReport_Plugins) list each class.

The reason is that the assembly is the unit Plugwright loads: one load context per assembly, metadata and dependencies of the assembly or package, and references between assemblies. A class cannot be loaded, verified or shared without the rest of its assembly.

So when parts of a feature must be turned on and off, watched or depended on separately, make them separate plugins, in separate assemblies. One class can also serve [several kinds](../how-to/combine-plugin-kinds.md), for example `IPlugin` and `IWebPlugin`, so several classes in one assembly are rarely needed.

If one of several classes fails with the failure mode `SkipAndLog`, only the services of that class are removed; the other classes keep running. The plugin's state is `Failed` nevertheless, because the plugin as a whole does not work as intended.

## Failures and the failure mode

Steps 2 to 6 can fail. Whether a failure stops the application depends on the plugin:

- A [required](../how-to/required-and-optional-plugins.md) plugin always stops it. Plugwright collects the problems of all required plugins and reports them in one exception: problems known after step 4 before any plugin is configured, failures in step 5 after all plugins are configured.
- Otherwise the plugin's failure mode decides, or the default [`PluginOptions.FailureMode`](../api/Plugwright.PluginOptions.md#Plugwright_PluginOptions_FailureMode): `FailFast` stops the application at the first failure, `SkipAndLog` records it and continues.

A plugin that is turned off is no failure. Turning a plugin off is a decision of whoever operates the application; plugins that depend on it simply do not load, and the application starts. Only if one of them is required does the start fail, because the decision then contradicts the code.

## Why some decisions only code can make

Configuration can turn plugins off, and the application can write the plugin settings file itself. Whether a plugin is required, which failure mode applies and which verifiers run are decisions about what the application needs to work safely. Code makes them, so that a writable file cannot turn off a required plugin or weaken verification. See the [security model](security-model.md).

## What can be taken back

When a plugin fails while it is configured, Plugwright restores the service registrations from before the plugin. The service collection is a list, so this is reliable. Configuration sources, builder properties and `ConfigureContainer` callbacks have no such simple state and stay.

After `Build()`, nothing can be taken back: the service container is fixed. A plugin that fails in a startup task keeps its services, and so do its dependents. That is why the catalog can change after startup, and why code that depends on an optional plugin should check its state.

## Why there is no unloading

States change only at the next start. Plugwright does not load, unload or restart plugins while the application runs:

- The service container cannot change after it is built.
- Hosted services, the middleware pipeline and Blazor routes are set up once at startup.
- An unloadable `AssemblyLoadContext` is only freed when no reference to it remains, and leaks go unnoticed.

A plugin that needs to pause can do so itself, for example with a feature flag in its settings read through `IOptionsMonitor`.

## See also

- [The two phases](two-phases.md)
- [Inspect plugins at runtime](../how-to/inspect-plugins-at-runtime.md)
