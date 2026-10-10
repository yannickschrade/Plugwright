using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using static Plugwright.IntegrationTests.TestPluginsFixture;

namespace Plugwright.IntegrationTests;

/// <summary>
/// Startup tasks of plugins from a directory: dependency order, and what a failure does to the plugins that depend on it.
/// The chain is Logging &lt;- Audit &lt;- AuditReport; each records "Name;" in TestPlugins:Startup.
/// </summary>
[Collection(LoadContextTests.Name)]
public sealed class StartupTaskTests(TestPluginsFixture testPlugins) : IDisposable
{
    private readonly TempDirectory _directory = new();

    private string PluginDirectory => Path.Combine(_directory.Path, "plugins");

    public void Dispose() => _directory.Dispose();

    [Fact]
    public async Task Startup_tasks_run_in_dependency_order()
    {
        // Installed in reverse order: the folder order must not matter.
        testPlugins.Install(AuditReport, PluginDirectory);
        testPlugins.Install(Audit, PluginDirectory);
        testPlugins.Install(Logging, PluginDirectory);

        using var app = await StartAsync();

        app.Services.GetRequiredService<IConfiguration>()["TestPlugins:Startup"].ShouldBe("Logging;Audit;AuditReport;");
    }

    [Fact]
    public async Task Failed_startup_skips_the_startup_tasks_of_dependents_and_the_host_runs()
    {
        testPlugins.Install(Logging, PluginDirectory);
        testPlugins.Install(Audit, PluginDirectory);
        testPlugins.Install(AuditReport, PluginDirectory);

        using var app = await StartAsync(
            builder => builder.Plugins.Options.FailureMode = PluginFailureMode.SkipAndLog,
            new() { [$"Plugwright:Plugins:{LoggingId}:Settings:FailStartup"] = "true" });

        app.Services.GetRequiredService<IConfiguration>()["TestPlugins:Startup"].ShouldBe("Logging;");
        var catalog = app.Services.GetRequiredService<IPluginCatalog>();
        catalog.Find(LoggingId).ShouldNotBeNull().State.ShouldBe(PluginState.Failed);
        catalog.Find(LoggingId)!.ErrorMessage.ShouldBe("Logging startup failed on purpose.");
        catalog.Find(AuditId).ShouldNotBeNull().State.ShouldBe(PluginState.DependencyUnavailable);
        catalog.Find(AuditReportId).ShouldNotBeNull().State.ShouldBe(PluginState.DependencyUnavailable);
        catalog.Find(AuditReportId)!.Reason.ShouldBe($"It depends on plugin '{AuditId}', which is not loaded because of its own dependencies.");
        app.Services.GetFakeLogCollector().GetSnapshot().Count(record => record.Id.Id == 1011).ShouldBe(2);
    }

    [Fact]
    public async Task Failed_startup_of_a_dependency_stops_a_required_dependent()
    {
        testPlugins.Install(Logging, PluginDirectory);
        testPlugins.Install(Audit, PluginDirectory);

        var exception = await Should.ThrowAsync<PluginException>(() => StartAsync(
            builder =>
            {
                builder.Plugins.Options.FailureMode = PluginFailureMode.SkipAndLog;
                builder.Plugins.Require(AuditId);
            },
            new() { [$"Plugwright:Plugins:{LoggingId}:Settings:FailStartup"] = "true" }));

        exception.Message.ShouldStartWith($"Required plugin '{AuditId}' cannot start because it depends on plugin '{LoggingId}'");
    }

    private async Task<IHost> StartAsync(Action<HostApplicationBuilder>? configure = null, Dictionary<string, string?>? settings = null)
    {
        var builder = TestHost.CreateBuilder(_directory.Path, settings);
        builder.Plugins.AddFromDirectory(PluginDirectory);
        configure?.Invoke(builder);
        builder.ApplyPlugins();
        var app = builder.Build();
        app.UsePlugins();
        await app.StartAsync(TestContext.Current.CancellationToken);
        return app;
    }
}
