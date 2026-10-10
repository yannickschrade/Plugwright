using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Plugwright.Tests.Plugins;

namespace Plugwright.Tests;

public sealed class EnablingTests
{
    private const string TestsId = "Plugwright.Tests";
    private const string EnabledKey = $"Plugwright:Plugins:{TestsId}:Enabled";

    [Fact]
    public void Disabled_plugin_added_in_code_registers_no_services_and_is_Disabled_in_the_catalog()
    {
        var builder = TestHost.CreateBuilder(new Dictionary<string, string?> { [EnabledKey] = "false" });
        builder.Plugins.Add<MarkerPlugin>();

        var app = Build(builder);

        app.Services.GetService<Marker>().ShouldBeNull();
        app.Services.GetRequiredService<PluginReport>().Plugins.ShouldBeEmpty();
        var plugin = app.Services.GetRequiredService<IPluginCatalog>().Plugins.ShouldHaveSingleItem();
        plugin.State.ShouldBe(PluginState.Disabled);
        plugin.Version.ShouldNotBeNull();
        plugin.AppliedHandlers.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("true", PluginState.Loaded)]
    [InlineData("True", PluginState.Loaded)]
    [InlineData("FALSE", PluginState.Disabled)]
    public void Enabled_accepts_true_and_false_in_any_case(string value, PluginState state)
    {
        var builder = TestHost.CreateBuilder(new Dictionary<string, string?> { [EnabledKey] = value });
        builder.Plugins.Add<MarkerPlugin>();

        Build(builder).Services.GetRequiredService<IPluginCatalog>().Plugins.ShouldHaveSingleItem().State.ShouldBe(state);
    }

    [Fact]
    public void Invalid_Enabled_value_throws()
    {
        var builder = TestHost.CreateBuilder(new Dictionary<string, string?> { [EnabledKey] = "no" });
        builder.Plugins.Options.FailureMode = PluginFailureMode.SkipAndLog;
        builder.Plugins.Add<MarkerPlugin>();

        var exception = Should.Throw<PluginException>(builder.ApplyPlugins);

        exception.Message.ShouldBe($"The configuration '{EnabledKey}' must be true or false, but is 'no'.");
    }

    [Fact]
    public void Environment_variable_overrides_appsettings()
    {
        // Same order as Host.CreateApplicationBuilder: appsettings.json, then environment variables.
        const string Prefix = "PLUGWRIGHT_ENABLING_TESTS_";
        Environment.SetEnvironmentVariable($"{Prefix}Plugwright__Plugins__{TestsId}__Enabled", "true");
        try
        {
            var builder = TestHost.CreateBuilder(new Dictionary<string, string?> { [EnabledKey] = "false" });
            builder.Configuration.AddEnvironmentVariables(Prefix);
            builder.Plugins.Add<MarkerPlugin>();

            Build(builder).Services.GetRequiredService<IPluginCatalog>().Plugins.ShouldHaveSingleItem().State.ShouldBe(PluginState.Loaded);
        }
        finally
        {
            Environment.SetEnvironmentVariable($"{Prefix}Plugwright__Plugins__{TestsId}__Enabled", null);
        }
    }

    private static IHost Build(HostApplicationBuilder builder)
    {
        builder.ApplyPlugins();
        var app = builder.Build();
        app.UsePlugins();
        return app;
    }
}
