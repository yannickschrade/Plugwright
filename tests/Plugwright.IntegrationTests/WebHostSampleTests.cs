extern alias WebHostSample;

using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Plugwright.IntegrationTests;

/// <summary>
/// samples/WebHost: a plugin added in code registers a service, a controller, an endpoint and middleware.
/// </summary>
public sealed class WebHostSampleTests : IDisposable
{
    private readonly WebApplicationFactory<WebHostSample::Program> _factory = new();

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task Host_endpoint_uses_the_service_of_the_plugin()
    {
        var response = await _factory.CreateClient().GetStringAsync("/", TestContext.Current.CancellationToken);

        response.ShouldBe("Hello, web host!");
    }

    [Fact]
    public async Task Plugin_endpoint_controller_and_middleware_work()
    {
        var client = _factory.CreateClient();
        var cancellationToken = TestContext.Current.CancellationToken;

        (await client.GetStringAsync("/greetings/Ada", cancellationToken)).ShouldBe("Hello, Ada!");
        (await client.GetStringAsync("/api/greetings?name=Grace", cancellationToken)).ShouldBe("Hello, Grace!");

        var response = await client.GetAsync("/", cancellationToken);
        response.Headers.GetValues("X-Greeted-By").ShouldBe(["Acme.Greetings"]);
    }

    [Fact]
    public async Task Controllers_of_a_plugin_turned_off_do_not_answer_although_the_host_references_its_assembly()
    {
        // The app references Acme.Greetings, so MVC would find GreetingsController on its own.
        using var factory = _factory.WithWebHostBuilder(builder =>
            builder.UseSetting("Plugwright:Plugins:Acme.Greetings:Enabled", "false"));
        var client = factory.CreateClient();
        var cancellationToken = TestContext.Current.CancellationToken;

        (await client.GetAsync("/api/greetings?name=Grace", cancellationToken)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await client.GetAsync("/greetings/Ada", cancellationToken)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}
