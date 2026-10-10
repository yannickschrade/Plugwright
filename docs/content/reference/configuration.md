# Configuration

Plugwright reads plugin settings from the application's normal configuration, [`Configuration`](https://learn.microsoft.com/dotnet/api/microsoft.extensions.hosting.ihostapplicationbuilder.configuration). If the application calls `AddFromConfiguration`, the same section also sets up sources, options and verifiers.

## The Plugwright section

```json
{
  "Plugwright": {
    "FailureMode": "FailFast",
    "SettingsFile": "pluginsettings.json",
    "Directories": [ "plugins" ],
    "Packages": [
      {
        "Path": "packages",
        "Feed": "https://api.nuget.org/v3/index.json",
        "DownloadDirectory": "plugin-package-downloads",
        "DownloadTimeout": "00:00:30",
        "VerifyDownloads": true
      }
    ],
    "Verification": {
      "Hash": { "LockFile": "plugins.lock.json" },
      "Certificate": {
        "TrustedThumbprints": [ "…" ],
        "TrustedPublishers": [ { "IssuerCertificate": "acme-ca.crt", "Subject": "CN=Acme Plugins, O=Acme" } ],
        "RevocationMode": "NoCheck"
      },
      "PackageSignature": { "TrustedThumbprints": [ "…" ] }
    },
    "Plugins": {
      "{PluginId}": { "Enabled": true, "Settings": { } }
    }
  }
}
```

| Key | Read by | Notes |
| --- | --- | --- |
| `FailureMode`, `SettingsFile`, `Directories`, `Packages` | `builder.Plugins.AddFromConfiguration` | `Packages` needs `builder.Plugins.AddNuGetSupport()`. An empty `SettingsFile` turns the plugin settings file off. |
| `Verification` | `builder.PluginVerifiers.AddFromConfiguration` | `PackageSignature` needs `builder.Plugins.AddNuGetSupport()`. |
| `Plugins:{PluginId}:Enabled`, `Plugins:{PluginId}:Settings` | `builder.ApplyPlugins()`, every plugin | Always read, see below. |

Unknown keys in these sections are errors. See [Configure plugins from appsettings.json](../how-to/configure-from-appsettings.md).

## Plugin settings

| Key | Meaning |
| --- | --- |
| `Plugwright:Plugins:{PluginId}:Settings` | The plugin's own settings. Available to the plugin as [`PluginContext.Configuration`](../api/Plugwright.PluginContext.md#Plugwright_PluginContext_Configuration). |
| `Plugwright:Plugins:{PluginId}:Enabled` | `false` turns the plugin off; it is neither verified nor loaded. Default `true`. Takes effect at the next start. See [Turn plugins on and off](../how-to/enable-and-disable-plugins.md). |
| `Plugwright:Plugins:{PluginId}:*` | Other keys next to `Settings` and `Enabled` are reserved for the plugin system. |

The plugin ID is the name of the plugin assembly. All plugin classes in one assembly share the same ID and the same section.

Whether a plugin is required, and its failure mode, are set in code only, with [`PluginPolicy`](../api/Plugwright.PluginPolicy.md). See [Required and optional plugins](../how-to/required-and-optional-plugins.md).

## The plugin settings file

`builder.ApplyPlugins()` adds the application-managed file `pluginsettings.json` as the last configuration source.

| Aspect | Behavior |
| --- | --- |
| Location | [`PluginOptions.SettingsFile`](../api/Plugwright.PluginOptions.md#Plugwright_PluginOptions_SettingsFile), default `pluginsettings.json`; relative paths are resolved against the content root. `null` turns the file off. The file need not exist. |
| Priority | Wins over all other sources, **including environment variables and command-line arguments**. |
| Keys | Only keys under `Plugwright:Plugins` are read; all others in the file are ignored. |
| Changes | Reloaded while the application runs. Settings bound with `IOptionsMonitor` update; `Enabled` takes effect at the next start. |
| Writing | [`IPluginSettingsStore`](../api/Plugwright.IPluginSettingsStore.md) rewrites the file; comments and formatting are lost. |

## Environment variables

Use a double underscore instead of `:`. A plugin ID with dots stays as it is:

```bash
export Plugwright__Plugins__Acme.Heartbeat__Settings__Interval=00:00:10
```

Some shells do not allow dots in variable names. In that case, set the variable from the process that starts the application (for example the service definition or a container's environment section), or use a JSON file.
