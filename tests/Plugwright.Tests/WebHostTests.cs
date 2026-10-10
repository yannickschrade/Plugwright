using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Plugwright.Tests.Plugins;

namespace Plugwright.Tests;

public sealed class WebHostTests
{
    [Fact]
    public async Task Extension_members_work_with_WebApplicationBuilder_and_WebApplication()
    {
        var builder = CreateWebBuilder();
        builder.Plugins.Add<ClockPlugin>();
        builder.PluginHandlers.Add<WebPluginHandler>();
        builder.ApplyPlugins();

        await using var app = builder.Build();
        app.UsePlugins();
        app.MapGet("/time", (PluginClock clock) => clock.Time);
        await app.StartAsync(TestContext.Current.CancellationToken);

        var response = await app.GetTestClient().GetAsync("/time", TestContext.Current.CancellationToken);

        (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).ShouldBe("12:00");
        response.Headers.GetValues("X-Clock").ShouldBe(["plugin"]);
    }

    [Fact]
    public async Task Web_plugin_does_not_need_to_implement_IPlugin()
    {
        var builder = CreateWebBuilder();
        builder.Plugins.Add<EndpointOnlyPlugin>();
        builder.PluginHandlers.Add<WebPluginHandler>();
        builder.ApplyPlugins();

        await using var app = builder.Build();
        app.UsePlugins();
        await app.StartAsync(TestContext.Current.CancellationToken);

        (await app.GetTestClient().GetStringAsync("/endpoint-only", TestContext.Current.CancellationToken)).ShouldBe("endpoint only");
    }

    [Fact]
    public void Web_plugin_in_a_host_that_is_not_a_web_application_throws()
    {
        var builder = TestHost.CreateBuilder();
        builder.Plugins.Add<EndpointOnlyPlugin>();
        builder.PluginHandlers.Add<WebPluginHandler>();
        builder.ApplyPlugins();
        var app = builder.Build();

        var exception = Should.Throw<PluginException>(() => app.UsePlugins());

        exception.Message.ShouldContain("not a web application");
        exception.Message.ShouldContain("WebApplication.CreateBuilder()");
    }

    [Fact]
    public void Component_assemblies_are_registered_without_any_Blazor_plugin()
    {
        var builder = CreateWebBuilder();
        builder.PluginHandlers.Add<BlazorPluginHandler>();
        builder.ApplyPlugins();
        var app = builder.Build();

        app.Services.GetRequiredService<IPluginComponentAssemblies>().Assemblies.ShouldBeEmpty();
    }

    [Fact]
    public void Component_assemblies_contain_each_Blazor_plugin_assembly_once()
    {
        var builder = CreateWebBuilder();
        builder.Plugins.Add<UiPlugin>().Add<EndpointOnlyPlugin>();
        builder.PluginHandlers.Add<WebPluginHandler>().Add<BlazorPluginHandler>();
        builder.ApplyPlugins();
        var app = builder.Build();

        app.Services.GetRequiredService<IPluginComponentAssemblies>().Assemblies.ShouldBe([typeof(UiPlugin).Assembly]);
    }

    [Fact]
    public async Task AddPluginAssemblies_without_the_Blazor_handler_throws()
    {
        var builder = CreateWebBuilder();
        builder.Services.AddRazorComponents();
        builder.ApplyPlugins();
        await using var app = builder.Build();
        app.UsePlugins();

        var exception = Should.Throw<InvalidOperationException>(
            () => app.MapRazorComponents<TestComponent>().AddPluginAssemblies(app));

        exception.Message.ShouldContain("BlazorPluginHandler");
    }

    private static WebApplicationBuilder CreateWebBuilder()
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseTestServer();
        return builder;
    }

    private sealed class TestComponent : Microsoft.AspNetCore.Components.IComponent
    {
        public void Attach(Microsoft.AspNetCore.Components.RenderHandle renderHandle)
        {
        }

        public Task SetParametersAsync(Microsoft.AspNetCore.Components.ParameterView parameters) => Task.CompletedTask;
    }
}
