# Test applications that use Plugwright

Applications with plugins are tested like any ASP.NET Core application, with `WebApplicationFactory` from `Microsoft.AspNetCore.Mvc.Testing`. Plugwright needs no test API of its own: turning plugins off, changing their settings and making them fail all work through ordinary configuration.

The tests below are in `samples/WebHost/WebHost.Tests` in the repository.

## Turn plugins off and change their settings

```csharp
--8<-- "samples/WebHost/WebHost.Tests/WebHostTests.cs:tests"
```

- `UseSetting` adds values to the host's configuration before `Program.cs` runs, so `builder.ApplyPlugins()` sees them. Use it for `Plugwright:Plugins:{PluginId}:Enabled` and `…:Settings:…`.
- Settings added later, for example with `ConfigureAppConfiguration`, are only applied when the host is built, after `ApplyPlugins()` has read the configuration.
- Assert on the plugin state through [`IPluginCatalog`](../api/Plugwright.IPluginCatalog.md) in `factory.Services`.
- The [plugin settings file](enable-and-disable-plugins.md#the-plugin-settings-file) wins over `UseSetting`. If your application writes one, point [`PluginOptions.SettingsFile`](../api/Plugwright.PluginOptions.md#Plugwright_PluginOptions_SettingsFile) to a test folder or set it to `null` in tests.

## Make a plugin fail on purpose

To test how your application behaves when a plugin fails, use a test plugin that fails when a setting says so. Plugwright's own tests use this one:

```csharp
--8<-- "tests/TestPlugins/Faulty/FaultyPlugin.cs"
```

A test then chooses where it fails, and checks that the application keeps running and shows the failure:

```csharp
--8<-- "tests/Plugwright.IntegrationTests/WebApplicationFactoryTests.cs:failing"
```

The same pattern checks the opposite: with the plugin [required](required-and-optional-plugins.md), creating the client throws a [`PluginException`](../api/Plugwright.PluginException.md).

## Test a plugin on its own

A plugin's `Configure` method can be tested without a host: create a [`PluginContext`](../api/Plugwright.PluginContext.md) with its public constructor and pass it to the plugin. See the example on [`PluginContext`](../api/Plugwright.PluginContext.md#Plugwright_PluginContext__ctor_Microsoft_Extensions_Hosting_IHostApplicationBuilder_Microsoft_Extensions_Configuration_IConfiguration_Plugwright_PluginMetadata_Microsoft_Extensions_Logging_ILogger_).

## See also

- [Turn plugins on and off](enable-and-disable-plugins.md)
- [Required and optional plugins](required-and-optional-plugins.md)
