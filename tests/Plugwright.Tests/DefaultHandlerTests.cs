using Microsoft.Extensions.DependencyInjection;
using Plugwright.Tests.Plugins;

namespace Plugwright.Tests;

public sealed class DefaultHandlerTests
{
    [Fact]
    public void ApplyPlugins_calls_Configure_and_registers_services()
    {
        var builder = TestHost.CreateBuilder();
        builder.Plugins.Add<MarkerPlugin>().Add<SecondMarkerPlugin>();

        builder.ApplyPlugins();
        var app = builder.Build();
        app.UsePlugins();

        app.Services.GetServices<Marker>().Select(marker => marker.Source)
            .ShouldBe([nameof(MarkerPlugin), nameof(SecondMarkerPlugin)]);
    }

    [Fact]
    public void Plugins_are_not_configured_before_ApplyPlugins()
    {
        var builder = TestHost.CreateBuilder();
        builder.Plugins.Add<MarkerPlugin>();

        var app = builder.Build();

        app.Services.GetService<Marker>().ShouldBeNull();
    }

    [Fact]
    public void Context_carries_builder_metadata_and_settings_section()
    {
        var builder = TestHost.CreateBuilder(new Dictionary<string, string?>
        {
            ["Plugwright:Plugins:Plugwright.Tests:Settings:Interval"] = "00:00:10",
            ["Plugwright:Plugins:Plugwright.Tests:Enabled"] = "true",
        });
        builder.Plugins.Add<ContextCapturingPlugin>();

        builder.ApplyPlugins();

        var context = ContextCapturingPlugin.GetContext(builder);
        context.Builder.ShouldBeSameAs(builder);
        context.Metadata.Id.ShouldBe("Plugwright.Tests");
        context.Metadata.Version.ShouldBe(typeof(ContextCapturingPlugin).Assembly.GetName().Version!.ToString());
        context.Configuration["Interval"].ShouldBe("00:00:10");
        context.Configuration["Enabled"].ShouldBeNull();
    }

    [Fact]
    public void Settings_section_is_empty_when_not_configured()
    {
        var builder = TestHost.CreateBuilder();
        builder.Plugins.Add<ContextCapturingPlugin>();

        builder.ApplyPlugins();

        ContextCapturingPlugin.GetContext(builder).Configuration.GetChildren().ShouldBeEmpty();
    }
}
