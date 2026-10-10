using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Testing;
using static Plugwright.IntegrationTests.TestPluginsFixture;

namespace Plugwright.IntegrationTests;

/// <summary>
/// Turning plugins off with Plugwright:Plugins:{Id}:Enabled, and what happens to the plugins that depend on them.
/// </summary>
[Collection(LoadContextTests.Name)]
public sealed class EnablingTests(TestPluginsFixture testPlugins) : IDisposable
{
    private const string GreeterPackage = "Plugwright.TestPackages.Greeter.1.0.0";
    private const string ReportsPackage = "Plugwright.TestPackages.Reports.1.0.0";
    private const string LibraryPackage = "Plugwright.TestPackages.Library.1.0.0";
    private const string GreeterPackageId = "Plugwright.TestPackages.Greeter";
    private const string ReportsPackageId = "Plugwright.TestPackages.Reports";

    private readonly TempDirectory _directory = new();

    private string PluginDirectory => Path.Combine(_directory.Path, "plugins");

    private string PackageDirectory => Path.Combine(_directory.Path, "packages");

    public void Dispose() => _directory.Dispose();

    [Fact]
    public void Disabled_plugin_folder_is_not_loaded_and_appears_with_its_metadata()
    {
        testPlugins.Install(Greeter, PluginDirectory);
        testPlugins.Install(Counter, PluginDirectory);

        var app = Build(builder => builder.Plugins.AddFromDirectory(PluginDirectory), Disable(GreeterId));

        var greeter = Catalog(app).Find(GreeterId).ShouldNotBeNull();
        greeter.State.ShouldBe(PluginState.Disabled);
        greeter.Version.ShouldBe("1.2.0");
        greeter.Reason.ShouldBe($"It is turned off in configuration (Plugwright:Plugins:{GreeterId}:Enabled).");
        Catalog(app).Find(CounterId).ShouldNotBeNull().State.ShouldBe(PluginState.Loaded);
        app.Services.GetRequiredService<PluginReport>().Plugins.ShouldAllBe(plugin => plugin.Metadata.Id == CounterId);
        AppDomain.CurrentDomain.GetAssemblies().ShouldNotContain(assembly =>
            !assembly.IsDynamic && assembly.Location.StartsWith(Path.Combine(PluginDirectory, GreeterId), StringComparison.Ordinal));
    }

    [Fact]
    public void Disabled_plugin_folder_is_not_verified_so_a_broken_one_does_not_stop_the_start()
    {
        testPlugins.Install(Greeter, PluginDirectory);

        // A hash verifier without lock file would fail every plugin it checks.
        var app = Build(
            builder =>
            {
                builder.Plugins.AddFromDirectory(PluginDirectory);
                builder.PluginVerifiers.AddHashVerifier();
            },
            Disable(GreeterId));

        var greeter = Catalog(app).Plugins.ShouldHaveSingleItem();
        greeter.State.ShouldBe(PluginState.Disabled);
        greeter.Version.ShouldBeNull();
        app.Services.GetRequiredService<PluginReport>().Verifications.ShouldBeEmpty();
    }

    [Fact]
    public void Dependents_of_a_disabled_plugin_are_DependencyUnavailable_without_failing_the_start()
    {
        testPlugins.Install(Logging, PluginDirectory);
        testPlugins.Install(Audit, PluginDirectory);
        testPlugins.Install(AuditReport, PluginDirectory);
        testPlugins.Install(Greeter, PluginDirectory);

        var app = Build(builder => builder.Plugins.AddFromDirectory(PluginDirectory), Disable(LoggingId));

        var catalog = Catalog(app);
        catalog.Find(LoggingId).ShouldNotBeNull().State.ShouldBe(PluginState.Disabled);
        var audit = catalog.Find(AuditId).ShouldNotBeNull();
        audit.State.ShouldBe(PluginState.DependencyUnavailable);
        audit.Reason.ShouldBe($"It depends on plugin '{LoggingId}', which is disabled.");
        var report = catalog.Find(AuditReportId).ShouldNotBeNull();
        report.State.ShouldBe(PluginState.DependencyUnavailable);
        report.Reason.ShouldBe($"It depends on plugin '{AuditId}', which is not loaded because of its own dependencies.");
        catalog.Find(GreeterId).ShouldNotBeNull().State.ShouldBe(PluginState.Loaded);
        app.Services.GetFakeLogCollector().GetSnapshot().ShouldNotContain(record => record.Id.Id == 1001);
    }

    [Fact]
    public void Disabled_plugin_package_is_neither_verified_nor_loaded_and_its_dependents_are_unavailable()
    {
        testPlugins.InstallPackage(GreeterPackage, PackageDirectory);
        testPlugins.InstallPackage(ReportsPackage, PackageDirectory);
        testPlugins.InstallPackage(LibraryPackage, PackageDirectory);

        var app = Build(
            builder =>
            {
                builder.Plugins.AddFromPackages(PackageDirectory);
                builder.Plugins.Options.FailureMode = PluginFailureMode.SkipAndLog;
                builder.PluginVerifiers.AddHashVerifier();
            },
            Disable(GreeterPackageId));

        var greeter = Catalog(app).Find(GreeterPackageId).ShouldNotBeNull();
        greeter.State.ShouldBe(PluginState.Disabled);
        greeter.Version.ShouldBe("1.0.0");
        Catalog(app).Find(ReportsPackageId).ShouldNotBeNull().State.ShouldNotBe(PluginState.Loaded);
        app.Services.GetRequiredService<PluginReport>().Verifications
            .ShouldNotContain(verification => verification.PluginName.StartsWith(GreeterPackageId, StringComparison.Ordinal));
    }

    [Fact]
    public void Plugin_settings_file_can_turn_a_plugin_off()
    {
        testPlugins.Install(Greeter, PluginDirectory);
        File.WriteAllText(
            Path.Combine(_directory.Path, "pluginsettings.json"),
            $$"""{ "Plugwright": { "Plugins": { "{{GreeterId}}": { "Enabled": false } } } }""");

        var app = Build(builder => builder.Plugins.AddFromDirectory(PluginDirectory), Enable(GreeterId));

        Catalog(app).Find(GreeterId).ShouldNotBeNull().State.ShouldBe(PluginState.Disabled);
    }

    private static Dictionary<string, string?> Disable(string pluginId) => new() { [$"Plugwright:Plugins:{pluginId}:Enabled"] = "false" };

    private static Dictionary<string, string?> Enable(string pluginId) => new() { [$"Plugwright:Plugins:{pluginId}:Enabled"] = "true" };

    private static IPluginCatalog Catalog(IHost app) => app.Services.GetRequiredService<IPluginCatalog>();

    private IHost Build(Action<HostApplicationBuilder> configure, Dictionary<string, string?> settings)
    {
        var builder = TestHost.CreateBuilder(_directory.Path, settings);
        configure(builder);
        builder.ApplyPlugins();
        var app = builder.Build();
        app.UsePlugins();
        return app;
    }
}
