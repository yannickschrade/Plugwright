using System.Net.Http.Json;
using static Plugwright.IntegrationTests.TestPluginsFixture;

namespace Plugwright.IntegrationTests;

/// <summary>
/// The catalog endpoint of a web host, read by another host with the <see cref="PluginStatus"/> DTO.
/// </summary>
[Collection(LoadContextTests.Name)]
public sealed class PluginCatalogEndpointTests(TestPluginsFixture testPlugins) : IDisposable
{
    private readonly TempDirectory _directory = new();

    private string PluginDirectory => Path.Combine(_directory.Path, "plugins");

    public void Dispose() => _directory.Dispose();

    [Fact]
    public async Task Endpoint_returns_all_plugins_with_their_state_but_no_error_messages()
    {
        using var host = StartHost();
        var client = host.CreateClient();

        var json = await client.GetStringAsync("/_plugwright/plugins", TestContext.Current.CancellationToken);
        var plugins = await client.GetFromJsonAsync<PluginStatus[]>("/_plugwright/plugins", TestContext.Current.CancellationToken);

        plugins.ShouldNotBeNull().Select(plugin => (plugin.Id, plugin.State)).ShouldBe(
            [(LoggingId, PluginState.Failed), (WebId, PluginState.Loaded), (GreeterId, PluginState.Disabled)],
            ignoreOrder: true);
        plugins.Single(plugin => plugin.Id == LoggingId).Reason.ShouldNotBeNull().ShouldContain("Phase 1 failed");
        plugins.ShouldAllBe(plugin => plugin.ErrorMessage == null);
        json.ShouldNotContain("errorMessage");
        json.ShouldContain("\"state\":\"Failed\"");
    }

    [Fact]
    public async Task Endpoint_can_include_error_messages()
    {
        using var host = StartHost();

        var plugins = await host.CreateClient().GetFromJsonAsync<PluginStatus[]>(
            "/_plugwright/plugins-with-errors", TestContext.Current.CancellationToken);

        plugins.ShouldNotBeNull().Single(plugin => plugin.Id == LoggingId).ErrorMessage.ShouldBe("Logging failed on purpose.");
    }

    [Fact]
    public async Task Endpoint_only_answers_GET()
    {
        using var host = StartHost();

        var response = await host.CreateClient().PostAsync("/_plugwright/plugins", content: null, TestContext.Current.CancellationToken);

        ((int)response.StatusCode).ShouldBe(405);
    }

    private WebTestHost StartHost()
    {
        testPlugins.Install(Web, PluginDirectory);
        testPlugins.Install(Logging, PluginDirectory);
        testPlugins.Install(Greeter, PluginDirectory);

        return new WebTestHost(new Dictionary<string, string?>
        {
            ["Plugwright:Directories:0"] = PluginDirectory,
            ["Plugwright:FailureMode"] = "SkipAndLog",
            [$"Plugwright:Plugins:{LoggingId}:Settings:Fail"] = "true",
            [$"Plugwright:Plugins:{GreeterId}:Enabled"] = "false",
        });
    }
}
