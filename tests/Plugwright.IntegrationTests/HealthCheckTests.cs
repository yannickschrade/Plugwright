using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using static Plugwright.IntegrationTests.TestPluginsFixture;

namespace Plugwright.IntegrationTests;

/// <summary>
/// The plugin health check with plugin dependencies: Audit depends on Logging.
/// </summary>
[Collection(LoadContextTests.Name)]
public sealed class HealthCheckTests(TestPluginsFixture testPlugins) : IDisposable
{
    private readonly TempDirectory _directory = new();

    private string PluginDirectory => Path.Combine(_directory.Path, "plugins");

    public void Dispose() => _directory.Dispose();

    [Fact]
    public async Task Plugin_that_misses_a_plugin_turned_off_is_healthy()
    {
        testPlugins.Install(Logging, PluginDirectory);
        testPlugins.Install(Audit, PluginDirectory);

        var entry = await CheckAsync(new Dictionary<string, string?> { [$"Plugwright:Plugins:{LoggingId}:Enabled"] = "false" });

        entry.Status.ShouldBe(HealthStatus.Healthy);
    }

    [Fact]
    public async Task Plugin_that_misses_a_dependency_is_reported()
    {
        testPlugins.Install(Audit, PluginDirectory);

        var entry = await CheckAsync();

        entry.Status.ShouldBe(HealthStatus.Degraded);
        entry.Data[AuditId].ShouldBe("DependencyUnavailable");
    }

    [Fact]
    public async Task Plugin_that_depends_on_a_failed_plugin_is_reported()
    {
        testPlugins.Install(Logging, PluginDirectory);
        testPlugins.Install(Audit, PluginDirectory);
        File.WriteAllText(Path.Combine(PluginDirectory, LoggingId, $"{LoggingId}.dll"), "not an assembly");

        var entry = await CheckAsync();

        entry.Status.ShouldBe(HealthStatus.Degraded);
        entry.Data[LoggingId].ShouldBe("Failed");
        entry.Data[AuditId].ShouldBe("DependencyUnavailable");
    }

    private async Task<HealthReportEntry> CheckAsync(IDictionary<string, string?>? settings = null)
    {
        var builder = TestHost.CreateBuilder(_directory.Path, settings);
        builder.Services.AddHealthChecks().AddPlugins();
        builder.Plugins.Options.FailureMode = PluginFailureMode.SkipAndLog;
        builder.Plugins.AddFromDirectory(PluginDirectory);
        builder.ApplyPlugins();
        using var app = builder.Build();
        app.UsePlugins();

        var report = await app.Services.GetRequiredService<HealthCheckService>().CheckHealthAsync(TestContext.Current.CancellationToken);
        return report.Entries["plugins"];
    }
}
