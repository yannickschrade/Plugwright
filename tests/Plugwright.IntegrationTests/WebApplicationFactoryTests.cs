using Microsoft.Extensions.DependencyInjection;
using static Plugwright.IntegrationTests.TestPluginsFixture;

namespace Plugwright.IntegrationTests;

/// <summary>
/// The pattern for testing an application that uses Plugwright: WebApplicationFactory, with plugins turned off or made
/// to fail through ordinary settings. No test API of Plugwright is needed.
/// </summary>
[Collection(LoadContextTests.Name)]
public sealed class WebApplicationFactoryTests(TestPluginsFixture testPlugins) : IDisposable
{
    private readonly TempDirectory _directory = new();

    private string PluginDirectory => Path.Combine(_directory.Path, "plugins");

    public void Dispose() => _directory.Dispose();

    [Fact]
    public async Task Plugin_turned_off_with_UseSetting_is_not_loaded()
    {
        using var host = StartHost(new() { [$"Plugwright:Plugins:{WebId}:Enabled"] = "false" });

        var response = await host.CreateClient().GetAsync("/web-plugin/endpoint", TestContext.Current.CancellationToken);

        ((int)response.StatusCode).ShouldBe(404);
        Catalog(host).Find(WebId).ShouldNotBeNull().State.ShouldBe(PluginState.Disabled);
    }

    [Fact]
    public void Dependents_of_a_plugin_turned_off_are_unavailable()
    {
        testPlugins.Install(Logging, PluginDirectory);
        testPlugins.Install(Audit, PluginDirectory);
        using var host = StartHost(new() { [$"Plugwright:Plugins:{LoggingId}:Enabled"] = "false" });

        host.CreateClient();

        Catalog(host).Find(AuditId).ShouldNotBeNull().State.ShouldBe(PluginState.DependencyUnavailable);
    }

    [Theory]
    [InlineData("Configure")]
    [InlineData("Startup")]
    public async Task Optional_plugin_that_fails_leaves_the_application_running(string throwIn)
    {
        testPlugins.Install(Faulty, PluginDirectory);
        using var host = StartHost(new()
        {
            ["Plugwright:FailureMode"] = "SkipAndLog",
            [$"Plugwright:Plugins:{FaultyId}:Settings:ThrowIn"] = throwIn,
        });

        var response = await host.CreateClient().GetStringAsync("/web-plugin/endpoint", TestContext.Current.CancellationToken);

        response.ShouldStartWith("endpoint:");
        var faulty = Catalog(host).Find(FaultyId).ShouldNotBeNull();
        faulty.State.ShouldBe(PluginState.Failed);
        faulty.ErrorMessage.ShouldNotBeNull().ShouldContain("on purpose");
    }

    [Theory]
    [InlineData("Configure")]
    [InlineData("Startup")]
    public void Required_plugin_that_fails_stops_the_application(string throwIn)
    {
        testPlugins.Install(Faulty, PluginDirectory);
        using var host = StartHost(new()
        {
            ["Plugwright:FailureMode"] = "SkipAndLog",
            ["TestWebHost:Require:0"] = FaultyId,
            [$"Plugwright:Plugins:{FaultyId}:Settings:ThrowIn"] = throwIn,
        });

        var exception = Should.Throw<PluginException>(() => host.CreateClient());

        exception.Message.ShouldStartWith($"Required plugin '{FaultyId}'");
    }

    [Fact]
    public void Required_plugin_that_is_missing_stops_the_application()
    {
        using var host = StartHost(new() { ["TestWebHost:Require:0"] = "Acme.Billing" });

        Should.Throw<PluginException>(() => host.CreateClient()).Message.ShouldContain("Required plugin 'Acme.Billing' was not found.");
    }

    private static IPluginCatalog Catalog(WebTestHost host) => host.Services.GetRequiredService<IPluginCatalog>();

    private WebTestHost StartHost(Dictionary<string, string?> settings)
    {
        testPlugins.Install(Web, PluginDirectory);
        settings["Plugwright:Directories:0"] = PluginDirectory;
        return new WebTestHost(settings);
    }
}
