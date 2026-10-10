# Configuration

Plugwright reads plugin settings from the application's normal configuration, <xref:Microsoft.Extensions.Hosting.IHostApplicationBuilder.Configuration>. If the application calls `AddFromConfiguration`, the same section also sets up sources, options and verifiers.

## The Plugwright section

```json
{
  "Plugwright": {
    "FailureMode": "FailFast",
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
      "{PluginId}": { "Settings": { } }
    }
  }
}
```

| Key | Read by | Notes |
| --- | --- | --- |
| `FailureMode`, `Directories`, `Packages` | `builder.Plugins.AddFromConfiguration` | `Packages` needs `builder.Plugins.AddNuGetSupport()`. |
| `Verification` | `builder.PluginVerifiers.AddFromConfiguration` | `PackageSignature` needs `builder.Plugins.AddNuGetSupport()`. |
| `Plugins:{PluginId}:Settings` | Every plugin | Always read, see below. |

Unknown keys in these sections are errors. See [Configure plugins from appsettings.json](../how-to/configure-from-appsettings.md).

## Plugin settings

| Key | Meaning |
| --- | --- |
| `Plugwright:Plugins:{PluginId}:Settings` | The plugin's own settings. Available to the plugin as <xref:Plugwright.PluginContext.Configuration>. |
| `Plugwright:Plugins:{PluginId}:*` | Other keys next to `Settings` are reserved for the plugin system. |

The plugin ID is the name of the plugin assembly. All plugin classes in one assembly share the same ID and the same section.

## Environment variables

Use a double underscore instead of `:`. A plugin ID with dots stays as it is:

```bash
export Plugwright__Plugins__Acme.Heartbeat__Settings__Interval=00:00:10
```

Some shells do not allow dots in variable names. In that case, set the variable from the process that starts the application (for example the service definition or a container's environment section), or use a JSON file.
