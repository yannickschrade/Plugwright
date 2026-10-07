# Log events

Plugwright logs with the category `Plugwright` and fixed event IDs. Use them to filter or alert on plugin events. Event IDs do not change between versions.

| Event ID | Name | Level | When |
| --- | --- | --- | --- |
| 1000 | `PluginLoaded` | Information | A plugin completed both phases. Written by `app.UsePlugins()`. |
| 1001 | `PluginSkipped` | Error | A plugin failed and was skipped because <xref:Plugwright.PluginOptions.FailureMode> is <xref:Plugwright.PluginFailureMode.SkipAndLog>. Includes the exception. |
| 1002 | `PluginDirectoryNotFound` | Warning | A directory added with `AddFromDirectory()` does not exist. |
| 1003 | `AssemblyOutsidePluginFolder` | Warning | A DLL lies directly in a plugin directory instead of in a plugin folder, and is ignored. |
| 1004 | `PluginVerificationDisabled` | Information | Plugins are loaded from a directory, but no verifier is registered. |
| 1005 | `PluginVerified` | Information | A plugin passed a verifier. One entry per plugin and verifier. |
| 1006 | `PluginVerificationFailed` | Error | A plugin failed a verifier. Includes the exception if the verifier threw one. |

## Structured values

| Event | Values |
| --- | --- |
| `PluginLoaded` | `PluginId`, `PluginVersion`, `PluginType`, `PluginSource` |
| `PluginSkipped` | `PluginId`, `PluginSource`, `Reason` |
| `PluginDirectoryNotFound` | `PluginDirectory` |
| `AssemblyOutsidePluginFolder` | `AssemblyPath`, `PluginDirectory` |
| `PluginVerificationDisabled` | – |
| `PluginVerified`, `PluginVerificationFailed` | `PluginName`, `PluginLocation`, `Verifier`, `Result` |

`PluginSource` is `added in code` for plugins added with `Add<T>()`, and the full path of the plugin folder for plugins loaded from a directory.

Entries from phase 1 are buffered and written when `app.UsePlugins()` runs. See [The two phases](../explanation/two-phases.md).
