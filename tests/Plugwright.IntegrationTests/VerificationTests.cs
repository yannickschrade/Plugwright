using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using static Plugwright.IntegrationTests.TestPluginsFixture;

namespace Plugwright.IntegrationTests;

[Collection(LoadContextTests.Name)]
public sealed class VerificationTests(TestPluginsFixture testPlugins) : IDisposable
{
    private readonly TempDirectory _directory = new();

    private string PluginDirectory => Path.Combine(_directory.Path, "plugins");

    private string LockFile => Path.Combine(_directory.Path, "plugins.lock.json");

    public void Dispose() => _directory.Dispose();

    [Fact]
    public void Without_verifiers_plugins_load_unverified_with_a_notice()
    {
        testPlugins.Install(Greeter, PluginDirectory);

        var app = BuildApp(builder => { });

        var notice = LogRecords(app).Where(record => record.Id.Id == 1004).ShouldHaveSingleItem();
        notice.Level.ShouldBe(LogLevel.Information);
        LogRecords(app).ShouldNotContain(record => record.Id.Id == 1005);
        LoadedPlugins(app).ShouldContain(GreeterId);
    }

    [Fact]
    public void Plugin_matching_the_lock_file_is_loaded_from_the_verified_bytes()
    {
        testPlugins.Install(Greeter, PluginDirectory);
        LockFileWriter.Write(LockFile, PluginDirectory);

        var app = BuildApp(builder => builder.PluginVerifiers.AddHashVerifier());

        var verified = LogRecords(app).Where(record => record.Id.Id == 1005).ShouldHaveSingleItem();
        verified.Message.ShouldContain(GreeterId);
        verified.Message.ShouldContain("HashVerifier");
        LogRecords(app).ShouldNotContain(record => record.Id.Id == 1004);
        LoadedPlugins(app).ShouldContain(GreeterId);
        Configuration(app)["TestPlugins:Greeter:TextLibVersion"].ShouldBe("1");

        // Loaded from memory, not from the files: no location, and nothing in this test's directory was loaded from disk.
        var greeterAssembly = AppDomain.CurrentDomain.GetAssemblies().Last(assembly => assembly.GetName().Name == GreeterId);
        greeterAssembly.Location.ShouldBeEmpty();
        AppDomain.CurrentDomain.GetAssemblies()
            .ShouldNotContain(assembly => !assembly.IsDynamic && assembly.Location.StartsWith(PluginDirectory, StringComparison.Ordinal));
    }

    [Fact]
    public void Verification_results_are_available_in_the_plugin_report()
    {
        testPlugins.Install(Greeter, PluginDirectory);
        var loggingFolder = testPlugins.Install(Logging, PluginDirectory);
        LockFileWriter.Write(LockFile, PluginDirectory);
        File.WriteAllText(Path.Combine(loggingFolder, "Extra.txt"), "not listed");

        var app = BuildApp(builder =>
        {
            builder.Plugins.Options.FailureMode = PluginFailureMode.SkipAndLog;
            builder.PluginVerifiers.AddHashVerifier();
        });

        var report = app.Services.GetRequiredService<PluginReport>();
        report.Verifications.Select(verification => (verification.PluginName, verification.Verifier, verification.Succeeded))
            .ShouldBe([(GreeterId, "HashVerifier", true), (LoggingId, "HashVerifier", false)]);
        report.Verifications[1].Message.ShouldBe("File 'Extra.txt' is not listed in the lock file.");
        report.Verifications[1].Location.ShouldBe(loggingFolder);
        report.SkippedPlugins.ShouldHaveSingleItem().PluginId.ShouldBe(LoggingId);
        report.Plugins.Select(plugin => plugin.Metadata.Id).Distinct().ShouldBe([GreeterId]);
    }

    [Fact]
    public void Dependent_plugins_share_types_when_loaded_from_verified_bytes()
    {
        testPlugins.Install(Audit, PluginDirectory);
        testPlugins.Install(Logging, PluginDirectory);
        LockFileWriter.Write(LockFile, PluginDirectory);

        var app = BuildApp(builder => builder.PluginVerifiers.AddHashVerifier());

        Configuration(app)["TestPlugins:Order"].ShouldBe("Logging;Audit;");
        app.Services.GetServices<IHostedService>().ShouldContain(service => service.GetType().Name == "AuditService");
    }

    [Fact]
    public void Changed_file_fails_before_anything_is_loaded()
    {
        var folder = testPlugins.Install(Greeter, PluginDirectory);
        LockFileWriter.Write(LockFile, PluginDirectory);
        var mainAssembly = Path.Combine(folder, $"{GreeterId}.dll");
        var bytes = File.ReadAllBytes(mainAssembly);
        bytes[^1] ^= 0xFF;
        File.WriteAllBytes(mainAssembly, bytes);

        var exception = Should.Throw<PluginException>(() => BuildApp(builder => builder.PluginVerifiers.AddHashVerifier()));

        exception.PluginId.ShouldBe(GreeterId);
        exception.Message.ShouldContain($"Verification by HashVerifier failed: File '{GreeterId}.dll' does not match its hash in the lock file.");
        AppDomain.CurrentDomain.GetAssemblies()
            .ShouldNotContain(assembly => !assembly.IsDynamic && assembly.Location.StartsWith(PluginDirectory, StringComparison.Ordinal));
    }

    [Fact]
    public void Added_file_fails()
    {
        var folder = testPlugins.Install(Greeter, PluginDirectory);
        LockFileWriter.Write(LockFile, PluginDirectory);
        File.WriteAllText(Path.Combine(folder, "Extra.dll"), "not listed");

        var exception = Should.Throw<PluginException>(() => BuildApp(builder => builder.PluginVerifiers.AddHashVerifier()));

        exception.Message.ShouldContain("File 'Extra.dll' is not listed in the lock file.");
    }

    [Fact]
    public void Plugin_missing_from_the_lock_file_fails()
    {
        LockFileWriter.Write(LockFile, Directory.CreateDirectory(Path.Combine(_directory.Path, "empty")).FullName);
        testPlugins.Install(Greeter, PluginDirectory);

        var exception = Should.Throw<PluginException>(() => BuildApp(builder => builder.PluginVerifiers.AddHashVerifier()));

        exception.Message.ShouldContain($"has no entry for plugin '{GreeterId}'");
    }

    [Fact]
    public void SkipAndLog_skips_failed_plugins_and_their_dependents()
    {
        testPlugins.Install(Audit, PluginDirectory);
        var loggingFolder = testPlugins.Install(Logging, PluginDirectory);
        testPlugins.Install(Greeter, PluginDirectory);
        LockFileWriter.Write(LockFile, PluginDirectory);
        File.WriteAllText(Path.Combine(loggingFolder, "Extra.txt"), "not listed");

        var app = BuildApp(builder =>
        {
            builder.Plugins.Options.FailureMode = PluginFailureMode.SkipAndLog;
            builder.PluginVerifiers.AddHashVerifier();
        });

        var failed = LogRecords(app).Where(record => record.Id.Id == 1006).ShouldHaveSingleItem();
        failed.Level.ShouldBe(LogLevel.Error);
        failed.Message.ShouldContain(LoggingId);
        LogRecords(app).Where(record => record.Id.Id == 1001).Select(record => record.Message)
            .ShouldContain(message => message.Contains(AuditId) && message.Contains($"It depends on plugin '{LoggingId}', which is not available."));
        LoadedPlugins(app).ShouldBe([GreeterId]);
    }

    [Fact]
    public void Verifiers_without_one_for_directories_fail_the_plugin()
    {
        testPlugins.Install(Greeter, PluginDirectory);

        var exception = Should.Throw<PluginException>(() => BuildApp(builder => builder.PluginVerifiers.Add<NothingVerifier>()));

        exception.Message.ShouldContain("No registered plugin verifier can verify plugins from this source (Directory ");
    }

    [Fact]
    public void All_responsible_verifiers_must_pass()
    {
        testPlugins.Install(Greeter, PluginDirectory);
        LockFileWriter.Write(LockFile, PluginDirectory);

        var exception = Should.Throw<PluginException>(() => BuildApp(builder =>
        {
            builder.PluginVerifiers.AddHashVerifier();
            builder.PluginVerifiers.Add<RejectingVerifier>();
        }));

        exception.Message.ShouldContain("Verification by RejectingVerifier failed: Rejected on purpose.");
    }

    private IHost BuildApp(Action<HostApplicationBuilder> configure)
    {
        var builder = TestHost.CreateBuilder(_directory.Path);
        configure(builder);
        builder.Plugins.AddFromDirectory(PluginDirectory);
        builder.ApplyPlugins();
        var app = builder.Build();
        app.UsePlugins();
        return app;
    }

    private static IConfiguration Configuration(IHost app) => app.Services.GetRequiredService<IConfiguration>();

    private static IReadOnlyList<FakeLogRecord> LogRecords(IHost app) => app.Services.GetFakeLogCollector().GetSnapshot();

    private static List<string> LoadedPlugins(IHost app) => LogRecords(app)
        .Where(record => record.Id.Id == 1000)
        .Select(record => record.StructuredState!.Single(pair => pair.Key == "PluginId").Value!)
        .Distinct()
        .ToList();

    private sealed class NothingVerifier : IPluginVerifier
    {
        public bool CanVerify(PluginSource source) => false;

        public PluginVerificationResult Verify(PluginVerificationRequest request) => throw new InvalidOperationException("Not responsible.");
    }

    private sealed class RejectingVerifier : IPluginVerifier
    {
        public bool CanVerify(PluginSource source) => source is PluginDirectorySource;

        public PluginVerificationResult Verify(PluginVerificationRequest request) => PluginVerificationResult.Failure("Rejected on purpose.");
    }
}
