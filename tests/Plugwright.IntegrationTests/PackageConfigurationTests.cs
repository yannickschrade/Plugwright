using Microsoft.Extensions.DependencyInjection;
using Plugwright.Cli;

namespace Plugwright.IntegrationTests;

[Collection(LoadContextTests.Name)]
public sealed class PackageConfigurationTests(TestPluginsFixture testPlugins) : IDisposable
{
    private const string Greeter = "Plugwright.TestPackages.Greeter.1.0.0";
    private const string LibraryV1 = "Plugwright.TestPackages.Library.1.0.0";
    private const string GreeterId = "Plugwright.TestPackages.Greeter";

    private readonly TempDirectory _directory = new();

    private string PackageDirectory => Path.Combine(_directory.Path, "packages");

    private string FeedDirectory => Path.Combine(_directory.Path, "feed");

    public void Dispose() => _directory.Dispose();

    [Fact]
    public void Directories_packages_with_feed_and_verifiers_come_from_configuration()
    {
        testPlugins.Install(TestPluginsFixture.Greeter, Path.Combine(_directory.Path, "plugins"));
        testPlugins.InstallPackage(Greeter, PackageDirectory);
        testPlugins.InstallPackage(LibraryV1, FeedDirectory);
        CreateLockFile("plugins", "packages");

        var report = Load(new()
        {
            ["Plugwright:Directories:0"] = "plugins",
            ["Plugwright:Packages:0:Path"] = "packages",
            ["Plugwright:Packages:0:Feed"] = FeedDirectory,
            ["Plugwright:Packages:0:VerifyDownloads"] = "false",
            ["Plugwright:Verification:Hash:LockFile"] = "plugins.lock.json",
        });

        report.Plugins.Select(plugin => plugin.Metadata.Id).Distinct().ShouldBe([TestPluginsFixture.GreeterId, GreeterId]);
        report.Verifications.Select(verification => verification.PluginName)
            .ShouldBe([TestPluginsFixture.GreeterId, Greeter]);
    }

    [Fact]
    public void Downloads_are_verified_unless_configured_otherwise()
    {
        testPlugins.InstallPackage(Greeter, PackageDirectory);
        testPlugins.InstallPackage(LibraryV1, FeedDirectory);
        CreateLockFile("packages");

        var exception = Should.Throw<PluginException>(() => Load(new()
        {
            ["Plugwright:Packages:0:Path"] = "packages",
            ["Plugwright:Packages:0:Feed"] = FeedDirectory,
            ["Plugwright:Verification:Hash"] = "true",
        }));

        exception.Message.ShouldContain($"has no entry for plugin '{LibraryV1}'");
    }

    [Fact]
    public void Package_folder_can_be_a_plain_path()
    {
        testPlugins.InstallPackage(Greeter, PackageDirectory);
        testPlugins.InstallPackage(LibraryV1, PackageDirectory);

        var report = Load(new() { ["Plugwright:Packages:0"] = "packages" });

        report.Plugins.ShouldHaveSingleItem().Metadata.Id.ShouldBe(GreeterId);
    }

    [Fact]
    public void Package_signature_verifier_comes_from_configuration()
    {
        testPlugins.InstallPackage(Greeter, PackageDirectory);

        var exception = Should.Throw<PluginException>(() => Load(new()
        {
            ["Plugwright:Packages:0"] = "packages",
            ["Plugwright:Verification:PackageSignature:TrustedThumbprints:0"] = new string('A', 64),
        }));

        exception.Message.ShouldContain("Verification by PackageSignatureVerifier failed");
    }

    [Fact]
    public void Unknown_package_setting_is_rejected()
    {
        var exception = Should.Throw<InvalidOperationException>(() => Load(new()
        {
            ["Plugwright:Packages:0:Path"] = "packages",
            ["Plugwright:Packages:0:Fead"] = FeedDirectory,
        }));

        exception.Message.ShouldStartWith("The configuration 'Plugwright:Packages:0:Fead' is not known.");
    }

    private void CreateLockFile(params string[] directories)
    {
        using var output = new StringWriter();
        CliApp.Run(["lock", .. directories.Select(directory => Path.Combine(_directory.Path, directory)), "-o", Path.Combine(_directory.Path, "plugins.lock.json")], output, output)
            .ShouldBe(CliApp.Success, output.ToString());
    }

    private PluginReport Load(Dictionary<string, string?> settings)
    {
        var builder = TestHost.CreateBuilder(_directory.Path, settings);

        builder.Plugins.AddNuGetSupport();
        var section = builder.Configuration.GetSection("Plugwright");
        builder.Plugins.AddFromConfiguration(section);
        builder.PluginVerifiers.AddFromConfiguration(section.GetSection("Verification"));

        builder.ApplyPlugins();
        var app = builder.Build();
        app.UsePlugins();
        return app.Services.GetRequiredService<PluginReport>();
    }
}
