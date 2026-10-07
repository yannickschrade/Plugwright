using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Logging;
using Plugwright.Tests.Plugins;

namespace Plugwright.Tests;

public sealed class FailureModeTests
{
    [Fact]
    public void Error_in_Configure_throws_PluginException_by_default()
    {
        var builder = TestHost.CreateBuilder();
        builder.Plugins.Add<ThrowingPlugin>();

        var exception = Should.Throw<PluginException>(() => builder.ApplyPlugins());

        exception.PluginId.ShouldBe("Plugwright.Tests");
        exception.Message.ShouldContain(nameof(ThrowingPlugin));
        exception.Message.ShouldContain("Configure went wrong");
        exception.InnerException.ShouldBeOfType<InvalidOperationException>();
    }

    [Fact]
    public void Error_in_constructor_throws_PluginException_by_default()
    {
        var builder = TestHost.CreateBuilder();
        builder.Plugins.Add<ThrowingConstructorPlugin>();

        var exception = Should.Throw<PluginException>(() => builder.ApplyPlugins());

        exception.Message.ShouldContain("Constructor went wrong");
        exception.InnerException.ShouldBeOfType<InvalidOperationException>();
    }

    [Theory]
    [InlineData(typeof(ThrowingPlugin))]
    [InlineData(typeof(ThrowingConstructorPlugin))]
    [InlineData(typeof(UnhandledPlugin))]
    public void SkipAndLog_skips_the_failing_plugin_and_keeps_the_others(Type failingPlugin)
    {
        var builder = TestHost.CreateBuilder();
        builder.Plugins.Options.FailureMode = PluginFailureMode.SkipAndLog;
        builder.Plugins.Add(failingPlugin).Add<MarkerPlugin>();

        builder.ApplyPlugins();
        var app = builder.Build();
        app.UsePlugins();

        app.Services.GetRequiredService<Marker>().Source.ShouldBe(nameof(MarkerPlugin));

        var logs = app.Services.GetFakeLogCollector().GetSnapshot();
        var skipped = logs.Where(record => record.Id.Id == 1001).ShouldHaveSingleItem();
        skipped.Level.ShouldBe(LogLevel.Error);
        skipped.Message.ShouldContain(failingPlugin.FullName!);
        logs.ShouldNotContain(record => record.Id.Id == 1000 && record.Message.Contains(failingPlugin.FullName!));
    }
}
