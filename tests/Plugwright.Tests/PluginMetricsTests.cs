using System.Diagnostics.Metrics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;
using Plugwright.Tests.Plugins;

namespace Plugwright.Tests;

public sealed class PluginMetricsTests
{
    [Fact]
    public void Gauge_counts_plugins_per_state()
    {
        var builder = TestHost.CreateBuilder();
        builder.Plugins.Options.FailureMode = PluginFailureMode.SkipAndLog;
        builder.Plugins.Add<ThrowingPlugin>();
        builder.ApplyPlugins();
        var app = builder.Build();
        app.UsePlugins();

        using var collector = new MetricCollector<int>(
            app.Services.GetRequiredService<IMeterFactory>(), "Plugwright", "plugwright.plugins");
        collector.RecordObservableInstruments();

        var measurement = collector.GetMeasurementSnapshot().ShouldHaveSingleItem();
        measurement.Value.ShouldBe(1);
        measurement.Tags["plugwright.plugin.state"].ShouldBe("Failed");
    }
}
