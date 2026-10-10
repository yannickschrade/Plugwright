using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Plugwright.Tests.Plugins;

namespace Plugwright.Tests;

public sealed class RequiredPluginTests
{
    private const string TestsId = "Plugwright.Tests";

    [Fact]
    public void Required_plugin_that_no_source_provides_throws()
    {
        var builder = TestHost.CreateBuilder();
        builder.Plugins.Require("Acme.Billing");

        var exception = Should.Throw<PluginException>(builder.ApplyPlugins);

        exception.Message.ShouldBe("Required plugin 'Acme.Billing' was not found. No plugin source provides it.");
        exception.PluginId.ShouldBe("Acme.Billing");
    }

    [Fact]
    public void All_required_plugins_with_problems_are_named_in_one_exception()
    {
        var builder = TestHost.CreateBuilder();
        builder.Plugins.Require("Acme.Billing").Require("Acme.Orders");

        var exception = Should.Throw<PluginException>(builder.ApplyPlugins);

        exception.Message.ShouldBe(
            "Required plugins cannot start:" + Environment.NewLine
            + "- Required plugin 'Acme.Billing' was not found. No plugin source provides it." + Environment.NewLine
            + "- Required plugin 'Acme.Orders' was not found. No plugin source provides it.");
        exception.PluginId.ShouldBeNull();
    }

    [Fact]
    public void Required_plugin_cannot_be_turned_off()
    {
        var builder = TestHost.CreateBuilder(new Dictionary<string, string?> { [$"Plugwright:Plugins:{TestsId}:Enabled"] = "false" });
        builder.Plugins.Add<MarkerPlugin>(policy => policy.Required = true);

        var exception = Should.Throw<PluginException>(builder.ApplyPlugins);

        exception.Message.ShouldBe(
            $"Required plugin '{TestsId}' is turned off in configuration (Plugwright:Plugins:{TestsId}:Enabled). "
            + "Required plugins cannot be turned off.");
    }

    [Fact]
    public void Required_plugin_that_throws_stops_the_application_even_with_SkipAndLog()
    {
        var builder = TestHost.CreateBuilder();
        builder.Plugins.Options.FailureMode = PluginFailureMode.SkipAndLog;
        builder.Plugins.Add<ThrowingPlugin>(policy => policy.Required = true);

        var exception = Should.Throw<PluginException>(builder.ApplyPlugins);

        exception.Message.ShouldStartWith($"Required plugin '{TestsId}' (added in code): Phase 1 failed");
        exception.InnerException.ShouldBeOfType<InvalidOperationException>().Message.ShouldBe("Configure went wrong");
    }

    [Fact]
    public void Optional_plugin_that_throws_is_skipped_although_the_global_mode_is_FailFast()
    {
        var builder = TestHost.CreateBuilder();
        builder.Plugins.Add<ThrowingPlugin>(policy => policy.FailureMode = PluginFailureMode.SkipAndLog);

        var app = Build(builder);

        var plugin = app.Services.GetRequiredService<IPluginCatalog>().Plugins.ShouldHaveSingleItem();
        plugin.State.ShouldBe(PluginState.Failed);
        plugin.IsRequired.ShouldBeFalse();
    }

    [Fact]
    public void Plugin_with_FailFast_throws_although_the_global_mode_is_SkipAndLog()
    {
        var builder = TestHost.CreateBuilder();
        builder.Plugins.Options.FailureMode = PluginFailureMode.SkipAndLog;
        builder.Plugins.Add<ThrowingPlugin>();
        builder.Plugins.Configure(TestsId, policy => policy.FailureMode = PluginFailureMode.FailFast);

        Should.Throw<PluginException>(builder.ApplyPlugins).PluginId.ShouldBe(TestsId);
    }

    [Fact]
    public void Services_of_a_plugin_that_fails_in_Configure_are_taken_back()
    {
        var builder = TestHost.CreateBuilder();
        builder.Plugins.Options.FailureMode = PluginFailureMode.SkipAndLog;
        builder.Plugins.Add<HalfConfiguredPlugin>();
        var servicesBefore = builder.Services.Count;

        var app = Build(builder);

        app.Services.GetService<Marker>().ShouldBeNull();
        builder.Services.Count(service => service.ServiceType == typeof(Marker)).ShouldBe(0);
        servicesBefore.ShouldBeGreaterThan(0);
    }

    [Fact]
    public void Catalog_shows_that_a_plugin_is_required()
    {
        var builder = TestHost.CreateBuilder();
        builder.Plugins.Add<MarkerPlugin>(policy => policy.Required = true);

        Build(builder).Services.GetRequiredService<IPluginCatalog>().Plugins.ShouldHaveSingleItem().IsRequired.ShouldBeTrue();
    }

    [Fact]
    public void Policy_cannot_be_changed_after_ApplyPlugins()
    {
        var builder = TestHost.CreateBuilder();
        builder.ApplyPlugins();

        Should.Throw<InvalidOperationException>(() => builder.Plugins.Require("Acme.Billing"));
    }

    private static IHost Build(HostApplicationBuilder builder)
    {
        builder.ApplyPlugins();
        var app = builder.Build();
        app.UsePlugins();
        return app;
    }
}
