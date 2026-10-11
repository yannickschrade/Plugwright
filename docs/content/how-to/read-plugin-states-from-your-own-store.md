# Read plugin states from your own store

Whether a plugin is on or off comes from configuration, the key `Plugwright:Plugins:{PluginId}:Enabled`. By default the application writes it to `pluginsettings.json`, see [Turn plugins on and off](enable-and-disable-plugins.md). If your application keeps the states somewhere else, for example in a database shared by several servers, supply them with a configuration provider. Plugwright needs no special interface for that. The complete project is in `samples/CustomActivation` in the repository.

## Write a configuration provider

A provider reads the states once and puts them into the configuration keys Plugwright reads:

```csharp
--8<-- "samples/CustomActivation/App/PluginStateConfigurationSource.cs:source"
```

## Add it before `ApplyPlugins()`

```csharp
--8<-- "samples/CustomActivation/App/Program.cs:host"
```

Things to keep in mind:

- **Add the source before `builder.ApplyPlugins()`.** Phase 1 decides which plugins are loaded, and it reads the configuration as it is at that moment.
- **Phase 1 has no service container.** The provider cannot receive services such as a `DbContext`. Create what it needs yourself, for example a database connection from a connection string in `builder.Configuration`.
- **Reading is synchronous.** `ConfigurationProvider.Load()` runs when the source is added. Keep it short: the application waits for it at every start.
- **`pluginsettings.json` wins.** It is the last configuration source, so a state saved there overrides your provider. If your store alone decides, turn the file off with `builder.Plugins.Options.SettingsFile = null`, and do not use `IPluginSettingsStore.SetEnabledAsync`.
- **Changes take effect at the next start**, as with every way of turning plugins on and off. Write the new state to your store, then [restart the application](restart-the-application.md).
- **Required plugins cannot be turned off.** If your store turns one off, the application does not start, as with any other configuration source.
- **Protect the store** like the plugin directory: whoever can change it decides which plugins run.

## See also

- [Turn plugins on and off](enable-and-disable-plugins.md)
- [Configuration reference](../reference/configuration.md)
