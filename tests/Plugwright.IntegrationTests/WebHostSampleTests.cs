extern alias WebHostSample;

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
}
