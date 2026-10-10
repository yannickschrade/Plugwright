using System.Diagnostics.Metrics;

namespace Plugwright;

/// <summary>
/// Metrics of the plugin system, in the meter <c>Plugwright</c>. Names are part of the documented behavior.
/// </summary>
internal static class PluginMetrics
{
    public const string MeterName = "Plugwright";

    /// <summary>
    /// Creates the gauge <c>plugwright.plugins</c>: the number of plugins per state, tagged <c>plugwright.plugin.state</c>.
    /// </summary>
    public static void Register(IMeterFactory meterFactory, IPluginCatalog catalog)
    {
        var meter = meterFactory.Create(MeterName);
        meter.CreateObservableGauge(
            "plugwright.plugins",
            () => catalog.Plugins
                .GroupBy(plugin => plugin.State)
                .Select(group => new Measurement<int>(group.Count(), new KeyValuePair<string, object?>("plugwright.plugin.state", group.Key.ToString()))),
            unit: "{plugin}",
            description: "The number of plugins in each state.");
    }
}
