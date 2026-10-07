using Microsoft.Extensions.DependencyInjection;
using Plugwright.Tests.Plugins;

namespace Plugwright.Tests;

public sealed class PluginReportTests
{
    [Fact]
    public void Report_lists_active_plugins_in_processing_order()
    {
        var builder = TestHost.CreateBuilder();
        builder.Plugins.Add<SecondMarkerPlugin>().Add<MarkerPlugin>();
        builder.ApplyPlugins();

        var report = builder.Build().Services.GetRequiredService<PluginReport>();

        report.Plugins.Select(plugin => plugin.PluginType).ShouldBe([typeof(SecondMarkerPlugin), typeof(MarkerPlugin)]);
        report.Plugins.ShouldAllBe(plugin => plugin.Metadata.Id == "Plugwright.Tests");
        report.SkippedPlugins.ShouldBeEmpty();
        report.Verifications.ShouldBeEmpty();
    }

    [Fact]
    public void Report_lists_skipped_plugins_with_their_reason()
    {
        var builder = TestHost.CreateBuilder();
        builder.Plugins.Options.FailureMode = PluginFailureMode.SkipAndLog;
        builder.Plugins.Add<ThrowingPlugin>().Add<MarkerPlugin>();
        builder.ApplyPlugins();

        var report = builder.Build().Services.GetRequiredService<PluginReport>();

        report.Plugins.ShouldHaveSingleItem().PluginType.ShouldBe(typeof(MarkerPlugin));
        var skipped = report.SkippedPlugins.ShouldHaveSingleItem();
        skipped.PluginId.ShouldBe("Plugwright.Tests");
        skipped.Source.ShouldBe("added in code");
        skipped.Reason.ShouldContain("Configure went wrong");
    }
}
