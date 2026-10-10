using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Plugwright.Tests.Plugins;

namespace Plugwright.Tests;

public sealed class LoggingTests
{
    [Fact]
    public void Phase_1_log_entries_are_written_when_UsePlugins_runs()
    {
        var builder = TestHost.CreateBuilder();
        builder.Plugins.Add<LoggingPlugin>();
        builder.ApplyPlugins();
        var app = builder.Build();
        var collector = app.Services.GetFakeLogCollector();

        collector.GetSnapshot().ShouldBeEmpty();

        app.UsePlugins();

        FakeLogRecord record = collector.GetSnapshot().Where(record => record.Message == "Configured during phase 1").ShouldHaveSingleItem();
        record.Category.ShouldBe(typeof(LoggingPlugin).FullName);
        record.Level.ShouldBe(LogLevel.Information);
    }

    [Fact]
    public void Plugin_logger_writes_directly_after_UsePlugins()
    {
        var builder = TestHost.CreateBuilder();
        builder.Plugins.Add<ContextCapturingPlugin>();
        builder.ApplyPlugins();
        var app = builder.Build();
        app.UsePlugins();
        var collector = app.Services.GetFakeLogCollector();
        collector.Clear();

        ContextCapturingPlugin.GetContext(builder).Logger.LogWarning("Written at runtime");

        collector.LatestRecord.Message.ShouldBe("Written at runtime");
    }

    [Fact]
    public void Loaded_plugins_are_logged_with_a_fixed_event_id()
    {
        var builder = TestHost.CreateBuilder();
        builder.Plugins.Add<MarkerPlugin>();
        builder.ApplyPlugins();
        var app = builder.Build();

        app.UsePlugins();

        FakeLogRecord record = app.Services.GetFakeLogCollector().GetSnapshot().Where(record => record.Id.Id == 1000).ShouldHaveSingleItem();
        record.Category.ShouldBe("Plugwright");
        record.Message.ShouldContain("Plugwright.Tests");
        record.Message.ShouldContain(typeof(MarkerPlugin).FullName!);
    }
}
