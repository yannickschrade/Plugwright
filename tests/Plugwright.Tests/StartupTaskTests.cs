using System.Net;
using System.Net.Sockets;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Testing;
using Plugwright.Tests.Plugins;

namespace Plugwright.Tests;

public sealed class StartupTaskTests
{
    private const string TestsId = "Plugwright.Tests";

    [Fact]
    public async Task Startup_tasks_run_in_order_each_in_its_own_scope()
    {
        var log = new StartupLog();
        var builder = TestHost.CreateBuilder();
        builder.Services.AddSingleton(log);
        builder.Plugins.Add<TwoStartupsPlugin>();

        using var app = Build(builder);
        await app.StartAsync(TestContext.Current.CancellationToken);

        log.Entries.Select(entry => entry.Split(':')[0]).ShouldBe(["First", "Second"]);
        log.Entries.Select(entry => entry.Split(':')[1]).Distinct().Count().ShouldBe(2);
        app.Services.GetFakeLogCollector().GetSnapshot().Where(record => record.Id.Id == 1009).Count().ShouldBe(2);
    }

    [Fact]
    public async Task Startup_tasks_do_not_run_before_the_host_starts()
    {
        var log = new StartupLog();
        var builder = TestHost.CreateBuilder();
        builder.Services.AddSingleton(log);
        builder.Plugins.Add<TwoStartupsPlugin>();

        using var app = Build(builder);

        log.Entries.ShouldBeEmpty();
        await app.StartAsync(TestContext.Current.CancellationToken);
        log.Entries.Count.ShouldBe(2);
    }

    [Fact]
    public async Task Web_server_does_not_answer_before_all_startup_tasks_are_done()
    {
        var gate = new StartupGate();
        var port = FreePort();
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseUrls($"http://127.0.0.1:{port}");
        builder.Services.AddSingleton(gate);
        builder.Plugins.Add<GatedStartupPlugin>();
        builder.ApplyPlugins();
        await using var app = builder.Build();
        app.UsePlugins();
        app.MapGet("/", () => "ready");
        using var client = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{port}") };
        var cancellationToken = TestContext.Current.CancellationToken;

        var starting = app.StartAsync(cancellationToken);
        await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);

        await Should.ThrowAsync<HttpRequestException>(() => client.GetStringAsync("/", cancellationToken));

        gate.Open.SetResult();
        await starting;
        (await client.GetStringAsync("/", cancellationToken)).ShouldBe("ready");
    }

    [Fact]
    public async Task Failing_startup_task_stops_the_start_by_default()
    {
        var builder = TestHost.CreateBuilder();
        builder.Plugins.Add<FailingStartupPlugin>();
        using var app = Build(builder);

        var exception = await Should.ThrowAsync<PluginException>(() => app.StartAsync(TestContext.Current.CancellationToken));

        exception.Message.ShouldContain("Startup task 'Plugwright.Tests.Plugins.FailingStartup' failed: Startup went wrong");
        exception.PluginId.ShouldBe(TestsId);
    }

    [Fact]
    public async Task Failing_startup_task_of_a_SkipAndLog_plugin_marks_it_failed_and_the_host_runs()
    {
        var builder = TestHost.CreateBuilder();
        builder.Plugins.Add<FailingStartupPlugin>(policy => policy.FailureMode = PluginFailureMode.SkipAndLog);
        using var app = Build(builder);

        await app.StartAsync(TestContext.Current.CancellationToken);

        var plugin = app.Services.GetRequiredService<IPluginCatalog>().Plugins.ShouldHaveSingleItem();
        plugin.State.ShouldBe(PluginState.Failed);
        plugin.ErrorMessage.ShouldBe("Startup went wrong");
        app.Services.GetFakeLogCollector().GetSnapshot().ShouldContain(record => record.Id.Id == 1010);
    }

    [Fact]
    public async Task Failing_startup_task_of_a_required_plugin_stops_the_start_even_with_SkipAndLog()
    {
        var builder = TestHost.CreateBuilder();
        builder.Plugins.Options.FailureMode = PluginFailureMode.SkipAndLog;
        builder.Plugins.Add<FailingStartupPlugin>(policy => policy.Required = true);
        using var app = Build(builder);

        var exception = await Should.ThrowAsync<PluginException>(() => app.StartAsync(TestContext.Current.CancellationToken));

        exception.Message.ShouldStartWith($"Required plugin '{TestsId}'");
    }

    [Fact]
    public async Task Startup_tasks_are_canceled_when_the_host_stops_while_starting()
    {
        var gate = new StartupGate();
        var builder = TestHost.CreateBuilder();
        builder.Services.AddSingleton(gate);
        builder.Plugins.Add<GatedStartupPlugin>();
        using var app = Build(builder);
        using var cancellation = new CancellationTokenSource();

        var starting = app.StartAsync(cancellation.Token);
        await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        await cancellation.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(() => starting);
    }

    [Fact]
    public void Without_startup_tasks_no_startup_service_is_registered()
    {
        var builder = TestHost.CreateBuilder();
        builder.Plugins.Add<MarkerPlugin>();

        using var app = Build(builder);

        app.Services.GetServices<IHostedService>().ShouldNotContain(service => service.GetType().Name == "PluginStartupRunner");
    }

    private static IHost Build(HostApplicationBuilder builder)
    {
        builder.ApplyPlugins();
        var app = builder.Build();
        app.UsePlugins();
        return app;
    }

    private static int FreePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }
}
