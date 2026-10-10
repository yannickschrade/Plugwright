using Microsoft.Extensions.DependencyInjection;
using static Plugwright.IntegrationTests.TestPluginsFixture;

namespace Plugwright.IntegrationTests;

/// <summary>
/// A web host loads a plugin from a directory that brings services, a controller, an endpoint, middleware and a page.
/// </summary>
[Collection(LoadContextTests.Name)]
public sealed class WebPluginTests(TestPluginsFixture testPlugins) : IDisposable
{
    private readonly TempDirectory _directory = new();

    private string PluginDirectory => Path.Combine(_directory.Path, "plugins");

    public void Dispose() => _directory.Dispose();

    [Theory]
    [InlineData("Before")]
    [InlineData("After")]
    public async Task Controller_in_a_plugin_assembly_is_reachable(string controllers)
    {
        using var host = StartHost(controllers);

        var response = await host.CreateClient().GetStringAsync("/web-plugin/controller", TestContext.Current.CancellationToken);

        response.ShouldBe("controller: Hello from the web plugin");
    }

    [Fact]
    public async Task Endpoint_from_MapEndpoints_is_reachable_and_uses_the_instance_from_phase_1()
    {
        using var host = StartHost(settings: new() { [$"Plugwright:Plugins:{WebId}:Settings:Greeting"] = "Hi" });

        var response = await host.CreateClient().GetStringAsync("/web-plugin/endpoint", TestContext.Current.CancellationToken);

        response.ShouldBe("endpoint: Hi, instance: Hi");
    }

    [Fact]
    public async Task Middleware_runs_at_the_position_of_UsePlugins()
    {
        using var host = StartHost();

        var response = await host.CreateClient().GetAsync("/web-plugin/endpoint", TestContext.Current.CancellationToken);

        response.Headers.GetValues("X-Pipeline").ShouldBe(["host-before", "plugin", "host-after"]);
    }

    [Fact]
    public async Task Page_of_a_plugin_is_routed()
    {
        using var host = StartHost(controllers: "None");

        var response = await host.CreateClient().GetStringAsync("/web-plugin/page", TestContext.Current.CancellationToken);

        response.ShouldContain("<h1>Web plugin page</h1>");
        response.ShouldContain("page: Hello from the web plugin");
        host.Services.GetRequiredService<IPluginComponentAssemblies>().Assemblies
            .ShouldHaveSingleItem().GetName().Name.ShouldBe(WebId);
    }

    [Fact]
    public async Task Services_controller_endpoint_and_page_of_one_plugin_work_together()
    {
        using var host = StartHost();
        var client = host.CreateClient();
        var cancellationToken = TestContext.Current.CancellationToken;

        (await client.GetStringAsync("/web-plugin/controller", cancellationToken)).ShouldStartWith("controller:");
        (await client.GetStringAsync("/web-plugin/endpoint", cancellationToken)).ShouldStartWith("endpoint:");
        (await client.GetStringAsync("/web-plugin/page", cancellationToken)).ShouldContain("Web plugin page");
        host.Services.GetRequiredService<PluginReport>().Plugins.ShouldHaveSingleItem().PluginType.Name.ShouldBe("WebPlugin");
    }

    [Theory]
    [InlineData("Before")]
    [InlineData("After")]
    public async Task OpenAPI_document_contains_the_plugin_controller_and_endpoint(string controllers)
    {
        using var host = StartHost(controllers);

        var document = await host.CreateClient().GetStringAsync("/openapi/v1.json", TestContext.Current.CancellationToken);

        document.ShouldContain("\"/web-plugin/controller\"");
        document.ShouldContain("\"/web-plugin/endpoint\"");
    }

    private WebTestHost StartHost(string controllers = "After", Dictionary<string, string?>? settings = null)
    {
        testPlugins.Install(Web, PluginDirectory);

        settings ??= [];
        settings["Plugwright:Directories:0"] = PluginDirectory;
        settings["TestWebHost:Controllers"] = controllers;
        return new WebTestHost(settings);
    }
}
