using System.Runtime.Loader;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Testing;

namespace Plugwright.IntegrationTests;

[Collection(LoadContextTests.Name)]
public sealed class PackageLoadingTests(TestPluginsFixture testPlugins) : IDisposable
{
    private const string Greeter = "Plugwright.TestPackages.Greeter.1.0.0";
    private const string Reports = "Plugwright.TestPackages.Reports.1.0.0";
    private const string LibraryV1 = "Plugwright.TestPackages.Library.1.0.0";
    private const string LibraryV2 = "Plugwright.TestPackages.Library.2.0.0";
    private const string GreeterId = "Plugwright.TestPackages.Greeter";
    private const string ReportsId = "Plugwright.TestPackages.Reports";

    private readonly TempDirectory _directory = new();

    private string PackageDirectory => Path.Combine(_directory.Path, "packages");

    public void Dispose() => _directory.Dispose();

    [Fact]
    public void Loads_plugin_packages_with_the_lowest_matching_library_version()
    {
        var greeterPackage = Install(Greeter);
        Install(LibraryV2);
        Install(LibraryV1);

        var app = BuildApp();

        var report = app.Services.GetRequiredService<PluginReport>();
        var plugin = report.Plugins.ShouldHaveSingleItem();
        plugin.Metadata.Id.ShouldBe(GreeterId);
        plugin.Metadata.Version.ShouldBe("1.0.0");
        plugin.Metadata.Description.ShouldBe("Greets from a plugin package.");
        Configuration(app)["TestPackages:Greeter:LibraryVersion"].ShouldBe("1");
        LogRecords(app).Where(record => record.Id.Id == 1000).ShouldHaveSingleItem().Message.ShouldContain(greeterPackage);
    }

    [Fact]
    public void Uses_a_higher_library_version_when_the_lowest_is_missing()
    {
        Install(Greeter);
        Install(LibraryV2);

        Configuration(BuildApp())["TestPackages:Greeter:LibraryVersion"].ShouldBe("2");
    }

    [Fact]
    public void Plugin_packages_are_loaded_from_memory_into_their_own_load_context()
    {
        Install(Greeter);
        Install(LibraryV1);

        var app = BuildApp();

        var assembly = app.Services.GetRequiredService<PluginReport>().Plugins.Single().PluginType.Assembly;
        assembly.Location.ShouldBeEmpty();
        AssemblyLoadContext.GetLoadContext(assembly)!.Name.ShouldBe(GreeterId);
    }

    [Fact]
    public void Dependent_plugin_package_comes_after_its_dependency_and_shares_its_types()
    {
        Install(Reports);
        Install(Greeter);
        Install(LibraryV1);

        var app = BuildApp();

        Configuration(app)["TestPackages:Order"].ShouldBe("Greeter;Reports;");
        Configuration(app)["TestPackages:Reports:Dependencies"].ShouldBe($"{GreeterId} [1.0.0, )");
        var reportsService = app.Services.GetServices<IHostedService>().Single(service => service.GetType().Name == "ReportsService");
        reportsService.GetType().GetProperty("Text")!.GetValue(reportsService).ShouldBe("Hello from library version 1");
    }

    [Fact]
    public void Plugin_reads_its_settings_by_package_id()
    {
        Install(Greeter);
        Install(LibraryV1);

        var app = BuildApp(new Dictionary<string, string?> { [$"Plugwright:Plugins:{GreeterId}:Settings:Message"] = "Hi" });

        Configuration(app)["TestPackages:Greeter:Message"].ShouldBe("Hi");
    }

    [Fact]
    public void Library_packages_alone_load_nothing()
    {
        Install(LibraryV1);

        var report = BuildApp().Services.GetRequiredService<PluginReport>();

        report.Plugins.ShouldBeEmpty();
        report.SkippedPlugins.ShouldBeEmpty();
    }

    [Fact]
    public void Missing_library_package_fails()
    {
        var greeterPackage = Install(Greeter);

        var exception = Should.Throw<PluginException>(() => BuildApp());

        exception.PluginId.ShouldBe(GreeterId);
        exception.Message.ShouldContain(greeterPackage);
        exception.Message.ShouldContain(
            "depends on package 'Plugwright.TestPackages.Library' [1.0.0, ), which is neither in the package folder nor provided by the host.");
    }

    [Fact]
    public void Missing_plugin_package_dependency_fails()
    {
        Install(Reports);

        var exception = Should.Throw<PluginException>(() => BuildApp());

        exception.PluginId.ShouldBe(ReportsId);
        exception.Message.ShouldContain($"depends on package '{GreeterId}'");
    }

    [Fact]
    public void Invalid_package_file_fails()
    {
        Directory.CreateDirectory(PackageDirectory);
        File.WriteAllText(Path.Combine(PackageDirectory, "Broken.1.0.0.nupkg"), "not a package");

        var exception = Should.Throw<PluginException>(() => BuildApp());

        exception.Message.ShouldContain("'Broken.1.0.0.nupkg' is not a valid NuGet package");
    }

    [Fact]
    public void SkipAndLog_skips_broken_plugin_packages_and_their_dependents()
    {
        Install(Reports);
        Install(Greeter);

        var app = BuildApp(skipAndLog: true);

        var report = app.Services.GetRequiredService<PluginReport>();
        report.Plugins.ShouldBeEmpty();
        report.SkippedPlugins.Select(skipped => skipped.PluginId).ShouldBe([GreeterId, ReportsId], ignoreOrder: true);
    }

    [Fact]
    public void Missing_package_folder_logs_a_warning()
    {
        var app = BuildApp();

        LogRecords(app).Where(record => record.Id.Id == 1002).ShouldHaveSingleItem().Message.ShouldContain(PackageDirectory);
    }

    [Fact]
    public void Packages_are_enumerated_as_sources()
    {
        var builder = TestHost.CreateBuilder(_directory.Path);

        builder.Plugins.AddFromPackages("packages");

        builder.Plugins.ShouldHaveSingleItem().ShouldBeOfType<PluginPackageSource>().Path.ShouldBe("packages");
    }

    private string Install(string package) => testPlugins.InstallPackage(package, PackageDirectory);

    private IHost BuildApp(IDictionary<string, string?>? settings = null, bool skipAndLog = false)
    {
        var builder = TestHost.CreateBuilder(_directory.Path, settings);
        if (skipAndLog)
        {
            builder.Plugins.Options.FailureMode = PluginFailureMode.SkipAndLog;
        }

        builder.Plugins.AddFromPackages("packages");
        builder.ApplyPlugins();
        var app = builder.Build();
        app.UsePlugins();
        return app;
    }

    private static IConfiguration Configuration(IHost app) => app.Services.GetRequiredService<IConfiguration>();

    private static IReadOnlyList<FakeLogRecord> LogRecords(IHost app) => app.Services.GetFakeLogCollector().GetSnapshot();
}
