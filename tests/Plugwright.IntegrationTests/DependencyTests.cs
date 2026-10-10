using System.Runtime.Loader;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Testing;
using static Plugwright.IntegrationTests.TestPluginsFixture;

namespace Plugwright.IntegrationTests;

[Collection(LoadContextTests.Name)]
public sealed class DependencyTests(TestPluginsFixture testPlugins) : IDisposable
{
    private readonly TempDirectory _directory = new();

    private string PluginDirectory => Path.Combine(_directory.Path, "plugins");

    public void Dispose() => _directory.Dispose();

    [Fact]
    public void Dependency_is_applied_first_even_if_it_sorts_later()
    {
        testPlugins.Install(Audit, PluginDirectory);
        testPlugins.Install(Logging, PluginDirectory);

        var app = BuildApp();

        Configuration(app)["TestPlugins:Order"].ShouldBe("Logging;Audit;");
    }

    [Fact]
    public void Dependent_plugin_uses_the_types_of_its_dependency()
    {
        // Audit's folder contains its own copy of the Logging assembly, which must not be used.
        File.Exists(Path.Combine(testPlugins.Install(Audit, PluginDirectory), $"{LoggingId}.dll")).ShouldBeTrue();
        testPlugins.Install(Logging, PluginDirectory);

        var app = BuildApp();

        // AuditService receives the LogSink registered by the Logging plugin; this only resolves if the types match.
        var auditService = app.Services.GetServices<IHostedService>().Single(service => service.GetType().Name == "AuditService");
        var sink = auditService.GetType().GetProperty("Sink")!.GetValue(auditService)!;
        AssemblyLoadContext.GetLoadContext(sink.GetType().Assembly)!.Name.ShouldBe(LoggingId);
        AssemblyLoadContext.GetLoadContext(auditService.GetType().Assembly)!.Name.ShouldBe(AuditId);
    }

    [Fact]
    public void Metadata_comes_from_the_assembly_and_fills_gaps_from_attributes()
    {
        testPlugins.Install(Audit, PluginDirectory);
        testPlugins.Install(Logging, PluginDirectory);

        var configuration = Configuration(BuildApp());

        configuration["TestPlugins:Logging:Version"].ShouldBe("1.5.0");
        configuration["TestPlugins:Logging:Description"].ShouldBe("Collects log entries for other test plugins.");
        configuration["TestPlugins:Logging:Authors"].ShouldBe("Logging team");
        configuration["TestPlugins:Audit:Dependencies"].ShouldBe($"{LoggingId} >= 1.0.0");
    }

    [Fact]
    public void Missing_dependency_fails_before_any_plugin_code_is_loaded()
    {
        testPlugins.Install(Audit, PluginDirectory);

        var exception = Should.Throw<PluginException>(BuildApp);

        exception.PluginId.ShouldBe(AuditId);
        exception.Message.ShouldContain($"It depends on plugin '{LoggingId}', which is not available.");
        AssembliesLoadedFrom(PluginDirectory).ShouldBeEmpty();
    }

    [Fact]
    public void Too_old_dependency_fails()
    {
        testPlugins.Install(NeedsNewLogging, PluginDirectory);
        testPlugins.Install(Logging, PluginDirectory);

        var exception = Should.Throw<PluginException>(BuildApp);

        exception.Message.ShouldContain($"It requires plugin '{LoggingId}' version 9.0.0 or later, but version 1.5.0 is available.");
    }

    [Fact]
    public void Cycle_fails_and_names_the_cycle()
    {
        testPlugins.Install(CycleA, PluginDirectory);
        testPlugins.Install(CycleB, PluginDirectory);

        var exception = Should.Throw<PluginException>(BuildApp);

        exception.Message.ShouldContain(
            "Plugins depend on each other in a cycle: Plugwright.TestPlugins.CycleA -> Plugwright.TestPlugins.CycleB -> Plugwright.TestPlugins.CycleA.");
    }

    [Fact]
    public void SkipAndLog_skips_dependents_of_a_failed_plugin()
    {
        testPlugins.Install(Audit, PluginDirectory);
        testPlugins.Install(Logging, PluginDirectory);
        testPlugins.Install(Greeter, PluginDirectory);

        var app = BuildApp(
            skipAndLog: true,
            settings: new Dictionary<string, string?> { [$"Plugwright:Plugins:{LoggingId}:Settings:Fail"] = "true" });

        var skipped = LogRecords(app).Where(record => record.Id.Id == 1001).Select(record => record.Message).ToList();
        skipped.Count.ShouldBe(2);
        skipped.ShouldContain(message => message.Contains(LoggingId) && message.Contains("Logging failed on purpose."));
        skipped.ShouldContain(message => message.Contains(AuditId) && message.Contains($"It depends on plugin '{LoggingId}', which is not loaded because it failed."));
        LogRecords(app).Where(record => record.Id.Id == 1000).ShouldAllBe(record => record.Message.Contains(GreeterId));

        // Audit was never loaded for execution.
        AssembliesLoadedFrom(Path.Combine(PluginDirectory, AuditId)).ShouldBeEmpty();

        var catalog = app.Services.GetRequiredService<IPluginCatalog>();
        catalog.Find(LoggingId).ShouldNotBeNull().State.ShouldBe(PluginState.Failed);
        catalog.Find(LoggingId)!.ErrorMessage.ShouldBe("Logging failed on purpose.");
        var audit = catalog.Find(AuditId).ShouldNotBeNull();
        audit.State.ShouldBe(PluginState.DependencyUnavailable);
        audit.Reason.ShouldBe($"It depends on plugin '{LoggingId}', which is not loaded because it failed.");
        audit.Dependencies.ShouldBe([LoggingId]);
        catalog.Find(GreeterId).ShouldNotBeNull().State.ShouldBe(PluginState.Loaded);
    }

    [Fact]
    public void SkipAndLog_skips_all_plugins_in_a_cycle_and_loads_the_others()
    {
        testPlugins.Install(CycleA, PluginDirectory);
        testPlugins.Install(CycleB, PluginDirectory);
        testPlugins.Install(Logging, PluginDirectory);

        var app = BuildApp(skipAndLog: true);

        LogRecords(app).Where(record => record.Id.Id == 1001).Count().ShouldBe(2);
        Configuration(app)["TestPlugins:Order"].ShouldBe("Logging;");
    }

    private IHost BuildApp() => BuildApp(skipAndLog: false);

    private IHost BuildApp(bool skipAndLog, IDictionary<string, string?>? settings = null)
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

    private static IConfiguration Configuration(IHost app) => app.Services.GetRequiredService<IConfiguration>();

    private static IReadOnlyList<FakeLogRecord> LogRecords(IHost app) => app.Services.GetFakeLogCollector().GetSnapshot();

    private static List<string> AssembliesLoadedFrom(string directory) => AppDomain.CurrentDomain.GetAssemblies()
        .Where(assembly => !assembly.IsDynamic && assembly.Location.StartsWith(directory, StringComparison.Ordinal))
        .Select(assembly => assembly.Location)
        .ToList();
}
