using Microsoft.Extensions.Configuration;

namespace CustomActivation.App;

// --8<-- [start:source]
/// <summary>
/// Supplies Plugwright:Plugins:{PluginId}:Enabled from the application's own store, here a database.
/// </summary>
public sealed class PluginStateConfigurationSource(string connectionString) : IConfigurationSource
{
    public IConfigurationProvider Build(IConfigurationBuilder builder) => new PluginStateConfigurationProvider(connectionString);
}

public sealed class PluginStateConfigurationProvider(string connectionString) : ConfigurationProvider
{
    // Runs once, when the source is added to the configuration, before builder.ApplyPlugins() reads it.
    public override void Load()
    {
        foreach (var (pluginId, enabled) in PluginStateDatabase.ReadAll(connectionString))
        {
            Data[$"Plugwright:Plugins:{pluginId}:Enabled"] = enabled ? "true" : "false";
        }
    }
}
// --8<-- [end:source]

/// <summary>
/// Stands for the application's database. A real application would query a table with plugin IDs and states.
/// </summary>
public static class PluginStateDatabase
{
    public static IEnumerable<(string PluginId, bool Enabled)> ReadAll(string connectionString) =>
    [
        ("Acme.Plugins.Orders", true),
        ("Acme.Plugins.Invoices", false),
    ];
}
