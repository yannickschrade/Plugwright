using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using static Plugwright.IntegrationTests.TestPluginsFixture;

namespace Plugwright.IntegrationTests;

/// <summary>
/// OptionalAudit depends on Logging optionally and ships its own copy of Logging's assembly.
/// </summary>
[Collection(LoadContextTests.Name)]
public sealed class OptionalDependencyTests(TestPluginsFixture testPlugins) : IDisposable
{
    private readonly TempDirectory _directory = new();

    private string PluginDirectory => Path.Combine(_directory.Path, "plugins");

    public void Dispose() => _directory.Dispose();

    [Fact]
    public void Present_optional_dependency_comes_first_and_shares_its_types()
    {
        testPlugins.Install(OptionalAudit, PluginDirectory);
        testPlugins.Install(Logging, PluginDirectory);

        var app = BuildApp();

        var configuration = app.Services.GetRequiredService<IConfiguration>();
        configuration["TestPlugins:Order"].ShouldBe("Logging;OptionalAudit;");
        configuration["TestPlugins:OptionalAudit:LoggingLoaded"].ShouldBe("True");
        // The service receives the LogSink registered by the Logging plugin: both use the same type.
        app.Services.GetServices<IHostedService>().ShouldContain(service => service.GetType().Name == "OptionalAuditService");
        var plugin = Catalog(app).Find(OptionalAuditId).ShouldNotBeNull();
        plugin.State.ShouldBe(PluginState.Loaded);
        plugin.Dependencies.ShouldBeEmpty();
        plugin.OptionalDependencies.ShouldBe([LoggingId]);
    }

    [Fact]
    public void Plugin_loads_without_its_optional_dependency_and_does_not_use_its_private_copy()
    {
        var folder = testPlugins.Install(OptionalAudit, PluginDirectory);
        File.Exists(Path.Combine(folder, $"{LoggingId}.dll")).ShouldBeTrue();

        var app = BuildApp();

        var configuration = app.Services.GetRequiredService<IConfiguration>();
        configuration["TestPlugins:OptionalAudit:LoggingLoaded"].ShouldBe("False");
        configuration["TestPlugins:OptionalAudit:LoggingTypeAvailable"].ShouldBe("False");
        Catalog(app).Find(OptionalAuditId).ShouldNotBeNull().State.ShouldBe(PluginState.Loaded);
    }

    [Fact]
    public void Plugin_loads_when_its_optional_dependency_is_turned_off()
    {
        testPlugins.Install(OptionalAudit, PluginDirectory);
        testPlugins.Install(Logging, PluginDirectory);

        var app = BuildApp(settings: new Dictionary<string, string?> { [$"Plugwright:Plugins:{LoggingId}:Enabled"] = "false" });

        app.Services.GetRequiredService<IConfiguration>()["TestPlugins:OptionalAudit:LoggingLoaded"].ShouldBe("False");
        Catalog(app).Find(OptionalAuditId).ShouldNotBeNull().State.ShouldBe(PluginState.Loaded);
    }

    [Fact]
    public void Plugin_loads_when_its_optional_dependency_fails()
    {
        testPlugins.Install(OptionalAudit, PluginDirectory);
        testPlugins.Install(Logging, PluginDirectory);

        var app = BuildApp(
            skipAndLog: true,
            new Dictionary<string, string?> { [$"Plugwright:Plugins:{LoggingId}:Settings:Fail"] = "true" });

        Catalog(app).Find(LoggingId).ShouldNotBeNull().State.ShouldBe(PluginState.Failed);
        Catalog(app).Find(OptionalAuditId).ShouldNotBeNull().State.ShouldBe(PluginState.Loaded);
        app.Services.GetRequiredService<IConfiguration>()["TestPlugins:OptionalAudit:LoggingLoaded"].ShouldBe("False");
    }

    [Fact]
    public async Task Failed_startup_task_of_an_optional_dependency_does_not_affect_the_plugin()
    {
        testPlugins.Install(OptionalAudit, PluginDirectory);
        testPlugins.Install(Logging, PluginDirectory);
        using var app = BuildApp(
            skipAndLog: true,
            new Dictionary<string, string?> { [$"Plugwright:Plugins:{LoggingId}:Settings:FailStartup"] = "true" });

        await app.StartAsync(TestContext.Current.CancellationToken);

        Catalog(app).Find(LoggingId).ShouldNotBeNull().State.ShouldBe(PluginState.Failed);
        Catalog(app).Find(OptionalAuditId).ShouldNotBeNull().State.ShouldBe(PluginState.Loaded);
        await app.StopAsync(TestContext.Current.CancellationToken);
    }

    private IHost BuildApp(bool skipAndLog = false, IDictionary<string, string?>? settings = null)
    {
        var builder = TestHost.CreateBuilder(_directory.Path, settings);
        if (skipAndLog)
        {
            builder.Plugins.Options.FailureMode = PluginFailureMode.SkipAndLog;
        }

        builder.Plugins.AddFromDirectory(PluginDirectory);
        builder.ApplyPlugins();
        var app = builder.Build();
        app.UsePlugins();
        return app;
    }

    private static IPluginCatalog Catalog(IHost app) => app.Services.GetRequiredService<IPluginCatalog>();
}
