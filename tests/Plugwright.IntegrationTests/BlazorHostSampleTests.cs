extern alias BlazorHostSample;

using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Plugwright.IntegrationTests;

/// <summary>
/// samples/BlazorHost: a Razor class library plugin brings the page /orders, a menu entry and a CSS file.
/// </summary>
public sealed class BlazorHostSampleTests : IDisposable
{
    private readonly WebApplicationFactory<BlazorHostSample::Program> _factory = new();

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task Plugin_page_is_reachable_directly()
    {
        var response = await _factory.CreateClient().GetStringAsync("/orders", TestContext.Current.CancellationToken);

        response.ShouldContain("<h1>Orders</h1>");
        response.ShouldContain("Espresso machine");
        response.ShouldContain("_content/Acme.Orders.Ui/orders.css");
    }

    [Fact]
    public async Task Menu_links_to_the_plugin_page()
    {
        var response = await _factory.CreateClient().GetStringAsync("/", TestContext.Current.CancellationToken);

        response.ShouldContain("href=\"orders\"");
    }

    [Fact]
    public async Task Static_file_of_the_plugin_is_served()
    {
        var response = await _factory.CreateClient().GetAsync("/_content/Acme.Orders.Ui/orders.css", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).ShouldContain(".orders");
    }
}
