using Microsoft.Extensions.Configuration;
using Plugwright.Tests.Plugins;

namespace Plugwright.Tests;

public sealed class ConfigurationTests
{
    [Fact]
    public void Failure_mode_and_directories_come_from_configuration()
    {
        var builder = Builder(new()
        {
            ["Plugwright:FailureMode"] = "skipandlog",
            ["Plugwright:Directories:0"] = "plugins",
            ["Plugwright:Directories:1"] = "/opt/more-plugins",
            ["Plugwright:Plugins:Acme:Settings:Interval"] = "5",
            ["Plugwright:Verification:Hash"] = "true",
        });
        builder.Plugins.Add<MarkerPlugin>();

        builder.Plugins.AddFromConfiguration(builder.Configuration.GetSection("Plugwright"));

        builder.Plugins.Options.FailureMode.ShouldBe(PluginFailureMode.SkipAndLog);
        builder.Plugins.Select(source => source.ToString())
            .ShouldBe([$"Type {typeof(MarkerPlugin).FullName}", "Directory plugins", "Directory /opt/more-plugins"]);
        builder.PluginVerifiers.ShouldBeEmpty();
    }

    [Fact]
    public void Missing_section_changes_nothing()
    {
        var builder = Builder([]);

        builder.Plugins.AddFromConfiguration(builder.Configuration.GetSection("Plugwright"));
        builder.PluginVerifiers.AddFromConfiguration(builder.Configuration.GetSection("Plugwright:Verification"));

        builder.Plugins.ShouldBeEmpty();
        builder.Plugins.Options.FailureMode.ShouldBe(PluginFailureMode.FailFast);
        builder.PluginVerifiers.ShouldBeEmpty();
    }

    [Fact]
    public void Invalid_failure_mode_is_rejected()
    {
        var builder = Builder(new() { ["Plugwright:FailureMode"] = "Ignore" });

        var exception = Should.Throw<InvalidOperationException>(() => builder.Plugins.AddFromConfiguration(builder.Configuration.GetSection("Plugwright")));

        exception.Message.ShouldBe("The configuration 'Plugwright:FailureMode' has the invalid value 'Ignore'. Valid values are: FailFast, SkipAndLog.");
    }

    [Fact]
    public void Unknown_key_is_rejected()
    {
        var builder = Builder(new() { ["Plugwright:Directorys:0"] = "plugins" });

        var exception = Should.Throw<InvalidOperationException>(() => builder.Plugins.AddFromConfiguration(builder.Configuration.GetSection("Plugwright")));

        exception.Message.ShouldBe("The configuration 'Plugwright:Directorys' is not known. Known keys are: Directories, FailureMode, Plugins, Verification.");
    }

    [Fact]
    public void Packages_need_NuGet_support()
    {
        var builder = Builder(new() { ["Plugwright:Packages:0"] = "packages" });

        var exception = Should.Throw<InvalidOperationException>(() => builder.Plugins.AddFromConfiguration(builder.Configuration.GetSection("Plugwright")));

        exception.Message.ShouldContain("needs Plugwright.NuGet");
        exception.Message.ShouldContain("builder.Plugins.AddNuGetSupport()");
    }

    [Fact]
    public void Verifiers_come_from_configuration()
    {
        var builder = Builder(new()
        {
            ["Plugwright:Verification:Hash:LockFile"] = "/etc/myapp/plugins.lock.json",
            ["Plugwright:Verification:Certificate:TrustedThumbprints:0"] = new string('A', 64),
        });

        builder.PluginVerifiers.AddFromConfiguration(builder.Configuration.GetSection("Plugwright:Verification"));

        builder.PluginVerifiers.Select(verifier => verifier.GetType()).ShouldBe([typeof(CertificateVerifier), typeof(HashVerifier)], ignoreOrder: true);
    }

    [Theory]
    [InlineData("true", 1)]
    [InlineData("True", 1)]
    [InlineData("false", 0)]
    public void Verifier_can_be_turned_on_with_defaults_or_off(string value, int expectedCount)
    {
        var builder = Builder(new() { ["Plugwright:Verification:Hash"] = value });

        builder.PluginVerifiers.AddFromConfiguration(builder.Configuration.GetSection("Plugwright:Verification"));

        builder.PluginVerifiers.Count.ShouldBe(expectedCount);
    }

    [Fact]
    public void Invalid_verifier_value_is_rejected()
    {
        var builder = Builder(new() { ["Plugwright:Verification:Hash"] = "maybe" });

        var exception = Should.Throw<InvalidOperationException>(() =>
            builder.PluginVerifiers.AddFromConfiguration(builder.Configuration.GetSection("Plugwright:Verification")));

        exception.Message.ShouldBe("The configuration 'Plugwright:Verification:Hash' must have settings, or be true or false, but is 'maybe'.");
    }

    [Fact]
    public void Unknown_verifier_is_rejected()
    {
        var builder = Builder(new() { ["Plugwright:Verification:Hsh"] = "true" });

        var exception = Should.Throw<InvalidOperationException>(() =>
            builder.PluginVerifiers.AddFromConfiguration(builder.Configuration.GetSection("Plugwright:Verification")));

        exception.Message.ShouldBe("The configuration 'Plugwright:Verification:Hsh' is not known. Known keys are: Certificate, Hash.");
    }

    [Fact]
    public void Package_signature_verifier_needs_NuGet_support()
    {
        var builder = Builder(new() { ["Plugwright:Verification:PackageSignature:TrustedThumbprints:0"] = new string('A', 64) });

        var exception = Should.Throw<InvalidOperationException>(() =>
            builder.PluginVerifiers.AddFromConfiguration(builder.Configuration.GetSection("Plugwright:Verification")));

        exception.Message.ShouldContain("needs Plugwright.NuGet");
    }

    [Fact]
    public void Configuration_cannot_be_read_after_ApplyPlugins()
    {
        var builder = Builder([]);
        builder.ApplyPlugins();

        Should.Throw<InvalidOperationException>(() => builder.Plugins.AddFromConfiguration(builder.Configuration.GetSection("Plugwright")));
        Should.Throw<InvalidOperationException>(() =>
            builder.PluginVerifiers.AddFromConfiguration(builder.Configuration.GetSection("Plugwright:Verification")));
    }

    private static Microsoft.Extensions.Hosting.HostApplicationBuilder Builder(Dictionary<string, string?> settings) =>
        TestHost.CreateBuilder(settings);
}
