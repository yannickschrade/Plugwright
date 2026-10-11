extern alias SplitHostsApiSample;

using Acme.Orders;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Plugwright.AspNetCore;

namespace Plugwright.IntegrationTests;

/// <summary>
/// samples/SplitHosts: the API host with Aspire service defaults and the backend part of the orders plugin.
/// </summary>
public sealed class SplitHostsSampleTests
{
    [Fact]
    public async Task Api_host_serves_the_plugin_endpoint_and_the_health_checks()
    {
        using var factory = new WebApplicationFactory<SplitHostsApiSample::Program>();
        var client = factory.CreateClient();
        var cancellationToken = TestContext.Current.CancellationToken;

        (await client.GetStringAsync("/api/orders", cancellationToken)).ShouldContain("Espresso machine");
        (await client.GetStringAsync("/health", cancellationToken)).ShouldBe("Healthy");
        factory.Services.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value.Registrations
            .Select(registration => registration.Name).ShouldBe(["self", "plugins", "orders"]);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Service_defaults_work_before_and_after_ApplyPlugins(bool serviceDefaultsFirst)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = Environments.Development });
        builder.WebHost.UseTestServer();
        if (serviceDefaultsFirst)
        {
            builder.AddServiceDefaults();
        }

        builder.Plugins.Add<OrdersPlugin>();
        builder.PluginHandlers.Add<WebPluginHandler>();
        builder.ApplyPlugins();

        if (!serviceDefaultsFirst)
        {
            builder.AddServiceDefaults();
        }

        await using var app = builder.Build();
        app.UsePlugins();
        app.MapDefaultEndpoints();
        await app.StartAsync(TestContext.Current.CancellationToken);

        (await app.GetTestClient().GetStringAsync("/health", TestContext.Current.CancellationToken)).ShouldBe("Healthy");
        app.Services.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value.Registrations
            .Select(registration => registration.Name).ShouldBe(["self", "orders"], ignoreOrder: true);
    }
}
