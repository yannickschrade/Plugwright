using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Plugwright.Cli;
using static Plugwright.IntegrationTests.TestPluginsFixture;

namespace Plugwright.IntegrationTests;

[Collection(LoadContextTests.Name)]
public sealed class CertificateVerificationTests(TestPluginsFixture testPlugins) : IDisposable
{
    private const string Publisher = "CN=Plugwright Test Plugins, O=Plugwright";

    private readonly TempDirectory _directory = new();
    private readonly StringWriter _output = new();
    private readonly StringWriter _error = new();

    private string PluginDirectory => Path.Combine(_directory.Path, "plugins");

    public void Dispose()
    {
        _directory.Dispose();
        _output.Dispose();
        _error.Dispose();
    }

    [Fact]
    public void Plugin_signed_with_the_cli_passes_with_a_trusted_thumbprint()
    {
        using var certificate = TestCertificates.CreateSelfSigned(Publisher);
        var folder = testPlugins.Install(Greeter, PluginDirectory);

        Sign(folder, certificate).ShouldBe(CliApp.Success, _error.ToString());

        _output.ToString().ShouldContain($"Signed {GreeterId} 1.2.0");
        var report = Load(options => options.TrustedThumbprints.Add(certificate.GetCertHashString(HashAlgorithmName.SHA256)));
        var verification = report.Verifications.ShouldHaveSingleItem();
        verification.Succeeded.ShouldBeTrue();
        verification.Verifier.ShouldBe("CertificateVerifier");
        verification.Message.ShouldContain("trusted by thumbprint");
        report.Plugins.ShouldContain(plugin => plugin.Metadata.Id == GreeterId);
    }

    [Fact]
    public void Publisher_trust_uses_intermediates_embedded_by_the_cli()
    {
        using var root = TestCertificates.CreateAuthority("CN=Plugwright Test Root");
        using var intermediate = TestCertificates.CreateAuthority("CN=Plugwright Test Signing CA", root);
        using var certificate = TestCertificates.CreateIssued(intermediate, Publisher);
        var rootFile = Path.Combine(_directory.Path, "root.crt");
        File.WriteAllText(rootFile, root.ExportCertificatePem());
        var folder = testPlugins.Install(Greeter, PluginDirectory);

        Sign(folder, certificate, intermediate, root).ShouldBe(CliApp.Success, _error.ToString());

        var report = Load(options => options.TrustedPublishers.Add(new TrustedPublisher { IssuerCertificate = rootFile, Subject = Publisher }));
        report.Verifications.ShouldHaveSingleItem().Message.ShouldContain("trusted as publisher");
    }

    [Fact]
    public void Password_can_come_from_the_environment()
    {
        using var certificate = TestCertificates.CreateSelfSigned(Publisher);
        var folder = testPlugins.Install(Greeter, PluginDirectory);
        var pfx = TestCertificates.WritePfx(Path.Combine(_directory.Path, "signing.pfx"), certificate);

        Environment.SetEnvironmentVariable(SignCommand.PasswordVariable, TestCertificates.Password);
        try
        {
            Run("sign", folder, "--certificate", pfx).ShouldBe(CliApp.Success, _error.ToString());
        }
        finally
        {
            Environment.SetEnvironmentVariable(SignCommand.PasswordVariable, null);
        }
    }

    [Fact]
    public void Changed_file_after_signing_fails()
    {
        using var certificate = TestCertificates.CreateSelfSigned(Publisher);
        var folder = testPlugins.Install(Greeter, PluginDirectory);
        Sign(folder, certificate).ShouldBe(CliApp.Success);
        File.AppendAllText(Path.Combine(folder, $"{GreeterId}.deps.json"), " ");

        var exception = Should.Throw<PluginException>(() => Load(options => options.TrustedThumbprints.Add(certificate.Thumbprint)));

        exception.Message.ShouldContain($"File '{GreeterId}.deps.json' does not match its hash in the signed manifest.");
    }

    [Fact]
    public void Plugin_signed_by_an_untrusted_certificate_fails()
    {
        using var trusted = TestCertificates.CreateSelfSigned(Publisher);
        using var other = TestCertificates.CreateSelfSigned(Publisher);
        var folder = testPlugins.Install(Greeter, PluginDirectory);
        Sign(folder, other).ShouldBe(CliApp.Success);

        var exception = Should.Throw<PluginException>(() => Load(options => options.TrustedThumbprints.Add(trusted.Thumbprint)));

        exception.Message.ShouldContain("is not trusted");
    }

    [Fact]
    public void Signing_again_replaces_the_manifest()
    {
        using var first = TestCertificates.CreateSelfSigned(Publisher);
        using var second = TestCertificates.CreateSelfSigned(Publisher);
        var folder = testPlugins.Install(Greeter, PluginDirectory);
        Sign(folder, first).ShouldBe(CliApp.Success);

        Sign(folder, second).ShouldBe(CliApp.Success);

        Load(options => options.TrustedThumbprints.Add(second.Thumbprint)).Verifications.ShouldHaveSingleItem().Succeeded.ShouldBeTrue();
    }

    [Fact]
    public void Hash_and_certificate_verifiers_can_be_combined()
    {
        using var certificate = TestCertificates.CreateSelfSigned(Publisher);
        Sign(testPlugins.Install(Audit, PluginDirectory), certificate).ShouldBe(CliApp.Success);
        Sign(testPlugins.Install(Logging, PluginDirectory), certificate).ShouldBe(CliApp.Success);
        var lockFile = Path.Combine(_directory.Path, "plugins.lock.json");
        Run("lock", PluginDirectory, "--output", lockFile).ShouldBe(CliApp.Success);

        var app = BuildApp(builder =>
        {
            builder.PluginVerifiers.AddHashVerifier();
            builder.PluginVerifiers.AddCertificateVerifier(options => options.TrustedThumbprints.Add(certificate.Thumbprint));
        });

        var report = app.Services.GetRequiredService<PluginReport>();
        report.Verifications.Count.ShouldBe(4);
        report.Verifications.ShouldAllBe(verification => verification.Succeeded);
        app.Services.GetRequiredService<IConfiguration>()["TestPlugins:Order"].ShouldBe("Logging;Audit;");
    }

    [Fact]
    public void Signing_fails_for_a_folder_without_main_assembly()
    {
        using var certificate = TestCertificates.CreateSelfSigned(Publisher);
        var folder = Directory.CreateDirectory(Path.Combine(PluginDirectory, "Empty")).FullName;

        Sign(folder, certificate).ShouldBe(CliApp.Failure);

        _error.ToString().ShouldContain("contains no 'Empty.dll'");
    }

    [Fact]
    public void Signing_fails_with_a_wrong_password()
    {
        using var certificate = TestCertificates.CreateSelfSigned(Publisher);
        var folder = testPlugins.Install(Greeter, PluginDirectory);
        var pfx = TestCertificates.WritePfx(Path.Combine(_directory.Path, "signing.pfx"), certificate);

        Run("sign", folder, "--certificate", pfx, "--password", "wrong").ShouldBe(CliApp.Failure);

        _error.ToString().ShouldContain("cannot be loaded");
    }

    [Fact]
    public void Signing_fails_without_a_private_key()
    {
        using var certificate = TestCertificates.CreateSelfSigned(Publisher);
        using var publicOnly = X509CertificateLoader.LoadCertificate(certificate.RawData);
        var folder = testPlugins.Install(Greeter, PluginDirectory);
        var pfx = TestCertificates.WritePfx(Path.Combine(_directory.Path, "public.pfx"), publicOnly);

        Run("sign", folder, "--certificate", pfx, "--password", TestCertificates.Password).ShouldBe(CliApp.Failure);

        _error.ToString().ShouldContain("no certificate with a private key");
    }

    [Theory]
    [InlineData("sign")]
    [InlineData("sign", "folder")]
    [InlineData("sign", "--certificate", "signing.pfx")]
    [InlineData("sign", "folder", "--certificate")]
    public void Invalid_sign_usage_fails_with_exit_code_2(params string[] args)
    {
        Run(args).ShouldBe(CliApp.UsageError);
    }

    private int Sign(string folder, X509Certificate2 certificate, params X509Certificate2[] chain)
    {
        var pfx = TestCertificates.WritePfx(Path.Combine(_directory.Path, $"{Guid.NewGuid():N}.pfx"), certificate, chain);
        return Run("sign", folder, "--certificate", pfx, "--password", TestCertificates.Password);
    }

    private PluginReport Load(Action<CertificateVerifierOptions> configure) =>
        BuildApp(builder => builder.PluginVerifiers.AddCertificateVerifier(configure)).Services.GetRequiredService<PluginReport>();

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

    private int Run(params string[] args) => CliApp.Run(args, _output, _error);
}
