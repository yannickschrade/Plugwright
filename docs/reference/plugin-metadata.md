# Plugin metadata

Every plugin has metadata: an ID, a version, an optional description and authors, and its dependencies. Plugins read their own metadata from <xref:Plugwright.PluginContext.Metadata>; handlers from <xref:Plugwright.PluginDescriptor.Metadata>.

Metadata belongs to the plugin assembly. All plugin classes in one assembly share it.

## Where it comes from

Plugwright reads metadata from the assembly itself, without running plugin code. For plugins from a directory, this happens before the plugin is loaded.

| Property | Source | Fallback |
| --- | --- | --- |
| <xref:Plugwright.PluginMetadata.Id> | Assembly name | – |
| <xref:Plugwright.PluginMetadata.Version> | `<Version>` of the project (`AssemblyInformationalVersion`), without build metadata after `+` | Assembly version |
| <xref:Plugwright.PluginMetadata.Description> | `<Description>` of the project | `[assembly: Plugin(Description = …)]` |
| <xref:Plugwright.PluginMetadata.Authors> | `<Authors>` or `<Company>` of the project | `[assembly: Plugin(Authors = …)]` |
| <xref:Plugwright.PluginMetadata.Dependencies> | References to other plugins' assemblies | Plus `[assembly: PluginDependency(…)]` |

Attributes only fill gaps. They never override what the project file sets. If the project file sets neither `<Authors>` nor `<Company>`, the .NET SDK uses the assembly name; Plugwright treats that as not set and uses the attribute.

```csharp
[assembly: Plugin(Description = "Sends a heartbeat to the monitoring system.", Authors = "Acme")]
[assembly: PluginDependency("Acme.Logging", MinVersion = "1.2.0")]
```

## Versions

Versions are semantic versions, such as `1.2.0` or `2.0.0-beta.1`, and are compared by SemVer 2.0 rules. Like NuGet, Plugwright also accepts a fourth number (`1.2.0.1`) and fewer than three numbers (`1.2` is `1.2.0`).

## IDs

The plugin ID names the plugin's configuration section, `Plugwright:Plugins:{PluginId}`. IDs are compared without regard to case, like configuration keys. Two plugins with the same ID cause a <xref:Plugwright.PluginException>.
