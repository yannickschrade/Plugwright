using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Plugwright.Cli;

namespace Plugwright.IntegrationTests;

/// <summary>
/// Downloads from a feed. A local folder serves as the feed; NuGet treats it like a server feed.
/// </summary>
[Collection(LoadContextTests.Name)]
public sealed class PackageFeedTests(TestPluginsFixture testPlugins) : IDisposable
{
    private const string Greeter = "Plugwright.TestPackages.Greeter.1.0.0";
    private const string Reports = "Plugwright.TestPackages.Reports.1.0.0";
    private const string LibraryV1 = "Plugwright.TestPackages.Library.1.0.0";
    private const string LibraryV2 = "Plugwright.TestPackages.Library.2.0.0";
    private const string GreeterId = "Plugwright.TestPackages.Greeter";

    // Nothing listens on the discard port, so connections are refused right away.
    private const string UnreachableFeed = "http://127.0.0.1:9/v3/index.json";

    private readonly TempDirectory _directory = new();

    private string PackageDirectory => Path.Combine(_directory.Path, "packages");

    private string FeedDirectory => Path.Combine(_directory.Path, "feed");

    private string DownloadDirectory => Path.Combine(_directory.Path, "plugin-package-downloads");

    public void Dispose() => _directory.Dispose();

    [Fact]
    public void Missing_library_is_downloaded_in_the_lowest_matching_version_and_stored()
    {
        Install(Greeter, PackageDirectory);
        Install(LibraryV2, FeedDirectory);
        Install(LibraryV1, FeedDirectory);

        var app = BuildApp(options => options.Feed = FeedDirectory);

        app.Services.GetRequiredService<IConfiguration>()["TestPackages:Greeter:LibraryVersion"].ShouldBe("1");
        Directory.GetFiles(DownloadDirectory).Select(Path.GetFileName).ShouldBe([$"{LibraryV1}.nupkg"]);
    }

    [Fact]
    public void Stored_downloads_are_used_without_the_feed()
    {
        Install(Greeter, PackageDirectory);
        Install(LibraryV1, FeedDirectory);
        BuildApp(options => options.Feed = FeedDirectory);

        var app = BuildApp(options => options.Feed = UnreachableFeed);

        app.Services.GetRequiredService<PluginReport>().Plugins.ShouldHaveSingleItem().Metadata.Id.ShouldBe(GreeterId);
    }

    [Fact]
    public void Unreachable_feed_fails_the_plugin()
    {
        Install(Greeter, PackageDirectory);

        var exception = Should.Throw<PluginException>(() => BuildApp(options =>
        {
            options.Feed = UnreachableFeed;
            options.DownloadTimeout = TimeSpan.FromSeconds(10);
        }));

        exception.PluginId.ShouldBe(GreeterId);
        exception.Message.ShouldContain("which is not in the package folder.");
        exception.Message.ShouldContain(UnreachableFeed);
    }

    [Fact]
    public void Unreachable_feed_skips_the_plugin_with_SkipAndLog()
    {
        Install(Greeter, PackageDirectory);

        var report = BuildApp(options => options.Feed = UnreachableFeed, skipAndLog: true).Services.GetRequiredService<PluginReport>();

        report.Plugins.ShouldBeEmpty();
        report.SkippedPlugins.ShouldHaveSingleItem().PluginId.ShouldBe(GreeterId);
    }

    [Fact]
    public void Feed_without_a_matching_version_fails()
    {
        Install(Greeter, PackageDirectory);
        Directory.CreateDirectory(FeedDirectory);

        var exception = Should.Throw<PluginException>(() => BuildApp(options => options.Feed = FeedDirectory));

        exception.Message.ShouldContain("has no version of package 'Plugwright.TestPackages.Library' that matches [1.0.0, )");
    }

    [Fact]
    public void Plugin_packages_are_not_downloaded()
    {
        Install(Reports, PackageDirectory);
        Install(Greeter, FeedDirectory);
        Install(LibraryV1, FeedDirectory);

        var exception = Should.Throw<PluginException>(() => BuildApp(options => options.Feed = FeedDirectory));

        exception.Message.ShouldContain("Plugin packages are not downloaded; install it in the package folder.");
        Directory.Exists(DownloadDirectory).ShouldBeFalse();
    }

    [Fact]
    public void Downloaded_packages_are_verified()
    {
        Install(Greeter, PackageDirectory);
        Install(LibraryV1, FeedDirectory);
        using (var output = new StringWriter())
        {
            CliApp.Run(["lock", PackageDirectory, "--output", Path.Combine(_directory.Path, "plugins.lock.json")], output, output)
                .ShouldBe(CliApp.Success);
        }

        var exception = Should.Throw<PluginException>(() => BuildApp(
            options => options.Feed = FeedDirectory,
            configure: builder => builder.PluginVerifiers.AddHashVerifier()));

        exception.Message.ShouldContain($"has no entry for plugin '{LibraryV1}'");
        Directory.Exists(DownloadDirectory).ShouldBeFalse();
    }

    private void Install(string package, string directory) => testPlugins.InstallPackage(package, directory);

    private IHost BuildApp(Action<PluginPackageOptions> options, bool skipAndLog = false, Action<HostApplicationBuilder>? configure = null)
    {
        var builder = TestHost.CreateBuilder(_directory.Path);
        if (skipAndLog)
        {
            builder.Plugins.Options.FailureMode = PluginFailureMode.SkipAndLog;
        }

        configure?.Invoke(builder);
        builder.Plugins.AddFromPackages("packages", options);
        builder.ApplyPlugins();
        var app = builder.Build();
        app.UsePlugins();
        return app;
    }
}
