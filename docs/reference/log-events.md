# Log events

Plugwright logs with the category `Plugwright` and fixed event IDs. Use them to filter or alert on plugin events. Event IDs do not change between versions.

| Event ID | Name | Level | When |
| --- | --- | --- | --- |
| 1000 | `PluginLoaded` | Information | A plugin completed both phases. Written by `app.UsePlugins()`. |
| 1001 | `PluginSkipped` | Error | A plugin failed and was skipped because <xref:Plugwright.PluginOptions.FailureMode> is <xref:Plugwright.PluginFailureMode.SkipAndLog>. Includes the exception. |

All entries carry the structured values `PluginId` and `PluginType`. `PluginLoaded` also carries `PluginVersion`, `PluginSkipped` carries `Reason`.

Entries from phase 1 are buffered and written when `app.UsePlugins()` runs. See [The two phases](../explanation/two-phases.md).
