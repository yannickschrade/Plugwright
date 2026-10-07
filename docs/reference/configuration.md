# Configuration

Plugwright reads plugin settings from the application's normal configuration, <xref:Microsoft.Extensions.Hosting.IHostApplicationBuilder.Configuration>.

## Layout

```json
{
  "Plugwright": {
    "Plugins": {
      "{PluginId}": {
        "Settings": { }
      }
    }
  }
}
```

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
