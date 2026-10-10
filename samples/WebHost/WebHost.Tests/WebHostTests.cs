using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Plugwright;
using Xunit;

namespace WebHost.Tests;

// --8<-- [start:tests]
public sealed class WebHostTests
{
    [Fact]
    public async Task Plugin_endpoint_answers()
    {
        await using var factory = new WebApplicationFactory<Program>();

        var greeting = await factory.CreateClient().GetStringAsync("/greetings/Ada", TestContext.Current.CancellationToken);

        Assert.Equal("Hello, Ada!", greeting);
    }

    [Fact]
    public async Task Plugin_can_be_turned_off_for_a_test()
    {
        // Ordinary configuration: UseSetting is applied before Program.cs calls ApplyPlugins().
        await using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder => builder.UseSetting("Plugwright:Plugins:Acme.Greetings:Enabled", "false"));

        var response = await factory.CreateClient().GetAsync("/greetings/Ada", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("The greetings plugin is turned off.", await factory.CreateClient().GetStringAsync("/", TestContext.Current.CancellationToken));
        var catalog = factory.Services.GetRequiredService<IPluginCatalog>();
        Assert.Equal(PluginState.Disabled, catalog.Find("Acme.Greetings")?.State);
    }

    [Fact]
    public async Task Plugin_settings_can_be_changed_for_a_test()
    {
        await using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder => builder.UseSetting("Plugwright:Plugins:Acme.Greetings:Settings:Salutation", "Hi"));

        var greeting = await factory.CreateClient().GetStringAsync("/greetings/Ada", TestContext.Current.CancellationToken);

        Assert.Equal("Hi, Ada!", greeting);
    }
}
// --8<-- [end:tests]
