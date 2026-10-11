# Turn plugins on and off

Every plugin can be turned off in configuration, without removing it. The change takes effect at the next start of the application; Plugwright does not load or unload plugins while the application runs.

## Turn a plugin off

Set `Plugwright:Plugins:{PluginId}:Enabled` to `false`. Without the key, a plugin is on:

```json
{
  "Plugwright": {
    "Plugins": {
      "Acme.Reports": { "Enabled": false }
    }
  }
}
```

The value comes from the application's merged configuration: `appsettings.json`, environment variables, command-line arguments and the plugin settings file. As environment variable:

```bash
Plugwright__Plugins__Acme.Reports__Enabled=false
```

A value other than `true` or `false` stops the application with a [`PluginException`](../api/Plugwright.PluginException.md).

## What off means

- No handler runs for the plugin: it registers no services, no middleware, no endpoints and no pages.
- Plugins from a directory or a package are not verified and not loaded. A broken or tampered plugin that is off does not stop the application.
- The plugin still appears in the [catalog](inspect-plugins-at-runtime.md) with the state `Disabled`. Its version is shown when it can be read without loading the plugin: for plugins added in code and packages always, for plugin folders only without verifiers.
- Plugins that depend on it, directly or transitively, are not loaded either. They appear as `DependencyUnavailable`, with the plugin that is off as reason. This is no failure: the [failure mode](handle-plugin-failures.md) does not apply.
- A [required plugin](required-and-optional-plugins.md) cannot be turned off. The application does not start and the exception names it. The same happens to a required plugin that depends on a plugin that is off.

!!! note

    The application's own code may use services of a plugin. When the plugin is off, those services are missing. Handle that in the host, or make the plugin required. See [Use plugin services in the host](use-in-a-web-app.md#use-plugin-services-in-the-host).

## Turn plugins on and off from the application

The application can change the state itself, for example on an administration page. [`IPluginSettingsStore`](../api/Plugwright.IPluginSettingsStore.md) writes the plugin settings file, `pluginsettings.json`:

```csharp
--8<-- "samples/BlazorHost/App/Components/Pages/Plugins.razor:toggle"
```

The page in `samples/BlazorHost` lists all plugins with a button each. After a restart, the plugins that were turned off are not loaded. To restart from the page, see [Restart the application](restart-the-application.md).

## The plugin settings file

`builder.ApplyPlugins()` adds `pluginsettings.json` as the last configuration source:

- Its values win over all other sources, **including environment variables and command-line arguments**. A plugin turned off there stays off, even if an environment variable turns it on.
- It only contributes keys under `Plugwright:Plugins`. A value elsewhere in the file is ignored, so the file cannot change other settings of the application or the verification of plugins.
- It is read from the content root by default and need not exist. Choose a writable location with [`PluginOptions.SettingsFile`](../api/Plugwright.PluginOptions.md#Plugwright_PluginOptions_SettingsFile), for example below `/var/lib` on Linux or `%ProgramData%` on Windows; `null` turns the file off.
- The application owns it: [`IPluginSettingsStore`](../api/Plugwright.IPluginSettingsStore.md) rewrites it on every save, so comments are lost.
- Changes are picked up while the application runs. Plugins that bind their settings with `IOptionsMonitor` see new settings right away; turning a plugin on or off waits for the restart.

## Turn plugins off in tests

Set the key like any other setting, for example with `UseSetting` in a `WebApplicationFactory`. See [Test applications that use Plugwright](test-apps.md).

## See also

- [Required and optional plugins](required-and-optional-plugins.md)
- [Configuration reference](../reference/configuration.md)
- [Security model](../explanation/security-model.md)
