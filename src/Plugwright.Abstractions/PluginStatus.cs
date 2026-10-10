using System.Text.Json.Serialization;

namespace Plugwright;

/// <summary>
/// The state of one plugin as the catalog endpoint of another host returns it (<c>MapPluginCatalog</c> in
/// Plugwright.AspNetCore). Use it to read that endpoint, for example from a UI host that hides pages whose backend
/// plugin is not loaded in the API host.
/// </summary>
/// <example>
/// <code language="csharp">
/// var plugins = await http.GetFromJsonAsync&lt;PluginStatus[]&gt;("/_plugwright/plugins");
/// var ordersLoaded = plugins?.Any(plugin =&gt; plugin.Id == "Acme.Orders" &amp;&amp; plugin.State == PluginState.Loaded) == true;
/// </code>
/// </example>
public sealed record PluginStatus
{
    /// <summary>
    /// Gets the plugin ID.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Gets the plugin version, if it is known.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Version { get; init; }

    /// <summary>
    /// Gets the plugin's state.
    /// </summary>
    public required PluginState State { get; init; }

    /// <summary>
    /// Gets why the plugin is not loaded, for people to read.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Reason { get; init; }

    /// <summary>
    /// Gets the message of the exception that made the plugin fail. Only present if the endpoint is set up to include
    /// error messages.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ErrorMessage { get; init; }
}
