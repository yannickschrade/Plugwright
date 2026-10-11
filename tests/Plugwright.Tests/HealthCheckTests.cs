using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Plugwright.Tests.Plugins;

namespace Plugwright.Tests;

public sealed class HealthCheckTests
{
    private const string TestsId = "Plugwright.Tests";

    [Fact]
    public async Task Healthy_when_all_plugins_run()
    {
        var builder = TestHost.CreateBuilder();
        builder.Services.AddHealthChecks().AddPlugins();
        builder.Plugins.Add<MarkerPlugin>();

        var entry = await CheckAsync(Build(builder));

        entry.Status.ShouldBe(HealthStatus.Healthy);
        entry.Data.ShouldBeEmpty();
    }

    [Fact]
    public async Task Healthy_when_a_plugin_is_turned_off()
    {
        var builder = TestHost.CreateBuilder(new Dictionary<string, string?> { [$"Plugwright:Plugins:{TestsId}:Enabled"] = "false" });
        builder.Services.AddHealthChecks().AddPlugins();
        builder.Plugins.Add<MarkerPlugin>();

        var entry = await CheckAsync(Build(builder));

        entry.Status.ShouldBe(HealthStatus.Healthy);
    }

    [Fact]
    public async Task Degraded_when_a_plugin_failed()
    {
        var builder = TestHost.CreateBuilder();
        builder.Services.AddHealthChecks().AddPlugins();
        builder.Plugins.Options.FailureMode = PluginFailureMode.SkipAndLog;
        builder.Plugins.Add<ThrowingPlugin>();

        var entry = await CheckAsync(Build(builder));

        entry.Status.ShouldBe(HealthStatus.Degraded);
        entry.Description.ShouldBe($"1 plugin(s) do not run because of an error: {TestsId} (Failed)");
        entry.Data[TestsId].ShouldBe("Failed");
    }

    [Fact]
    public async Task Reports_the_configured_failure_status()
    {
        var builder = TestHost.CreateBuilder();
        builder.Services.AddHealthChecks().AddPlugins("my-plugins", HealthStatus.Unhealthy, ["ready"]);
        builder.Plugins.Options.FailureMode = PluginFailureMode.SkipAndLog;
        builder.Plugins.Add<ThrowingPlugin>();

        var report = await Build(builder).Services.GetRequiredService<HealthCheckService>()
            .CheckHealthAsync(TestContext.Current.CancellationToken);

        var entry = report.Entries["my-plugins"];
        entry.Status.ShouldBe(HealthStatus.Unhealthy);
        entry.Tags.ShouldBe(["ready"]);
    }

    [Fact]
    public async Task Degraded_when_a_startup_task_failed()
    {
        var builder = TestHost.CreateBuilder();
        builder.Services.AddHealthChecks().AddPlugins();
        builder.Plugins.Options.FailureMode = PluginFailureMode.SkipAndLog;
        builder.Plugins.Add<FailingStartupPlugin>();
        using var app = Build(builder);

        (await CheckAsync(app)).Status.ShouldBe(HealthStatus.Healthy);
        await app.StartAsync(TestContext.Current.CancellationToken);

        (await CheckAsync(app)).Status.ShouldBe(HealthStatus.Degraded);
        await app.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Check_without_ApplyPlugins_reports_the_failure_status()
    {
        var builder = TestHost.CreateBuilder();
        builder.Services.AddHealthChecks().AddPlugins();
        using var app = builder.Build();

        var entry = await CheckAsync(app);

        entry.Status.ShouldBe(HealthStatus.Degraded);
        entry.Description.ShouldBe("The plugin health check needs builder.ApplyPlugins() before builder.Build().");
    }

    private static async Task<HealthReportEntry> CheckAsync(IHost app)
    {
        var report = await app.Services.GetRequiredService<HealthCheckService>().CheckHealthAsync(TestContext.Current.CancellationToken);
        return report.Entries["plugins"];
    }

    private static IHost Build(HostApplicationBuilder builder)
    {
        builder.ApplyPlugins();
        var app = builder.Build();
        app.UsePlugins();
        return app;
    }
}
