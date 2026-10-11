# Plugin metadata

Every plugin has metadata: an ID, a version, an optional description and authors, and its dependencies. Plugins read their own metadata from [`PluginContext.Metadata`](../api/Plugwright.PluginContext.md#Plugwright_PluginContext_Metadata); handlers from [`PluginDescriptor.Metadata`](../api/Plugwright.PluginDescriptor.md#Plugwright_PluginDescriptor_Metadata).

Metadata belongs to the plugin assembly. All plugin classes in one assembly share it.

## Where it comes from

Plugwright reads metadata from the assembly itself, without running plugin code. For plugins from a directory, this happens before the plugin is loaded.

| Property | Source | Fallback |
| --- | --- | --- |
| [`PluginMetadata.Id`](../api/Plugwright.PluginMetadata.md#Plugwright_PluginMetadata_Id) | Assembly name | – |
| [`PluginMetadata.Version`](../api/Plugwright.PluginMetadata.md#Plugwright_PluginMetadata_Version) | `<Version>` of the project (`AssemblyInformationalVersion`), without build metadata after `+` | Assembly version |
| [`PluginMetadata.Description`](../api/Plugwright.PluginMetadata.md#Plugwright_PluginMetadata_Description) | `<Description>` of the project | `[assembly: Plugin(Description = …)]` |
| [`PluginMetadata.Authors`](../api/Plugwright.PluginMetadata.md#Plugwright_PluginMetadata_Authors) | `<Authors>` or `<Company>` of the project | `[assembly: Plugin(Authors = …)]` |
| [`PluginMetadata.Dependencies`](../api/Plugwright.PluginMetadata.md#Plugwright_PluginMetadata_Dependencies) | References to other plugins' assemblies | Plus `[assembly: PluginDependency(…)]` |

Attributes only fill gaps. They never override what the project file sets. If the project file sets neither `<Authors>` nor `<Company>`, the .NET SDK uses the assembly name; Plugwright treats that as not set and uses the attribute.

```csharp
[assembly: Plugin(Description = "Sends a heartbeat to the monitoring system.", Authors = "Acme")]
[assembly: PluginDependency("Acme.Logging", MinVersion = "1.2.0")]
[assembly: PluginDependency("Acme.Reports", IsOptional = true)]
```

A dependency with `IsOptional = true` lets the plugin run without the other plugin. See [Make a dependency optional](../how-to/declare-plugin-dependencies.md#make-a-dependency-optional).

## Versions

Versions are semantic versions, such as `1.2.0` or `2.0.0-beta.1`, and are compared by SemVer 2.0 rules. Like NuGet, Plugwright also accepts a fourth number (`1.2.0.1`) and fewer than three numbers (`1.2` is `1.2.0`).

## IDs

The plugin ID names the plugin's configuration section, `Plugwright:Plugins:{PluginId}`. IDs are compared without regard to case, like configuration keys. Two plugins with the same ID cause a [`PluginException`](../api/Plugwright.PluginException.md).
