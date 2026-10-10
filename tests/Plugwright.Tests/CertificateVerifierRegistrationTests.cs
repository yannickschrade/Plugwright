using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace Plugwright.Tests;

public sealed class CertificateVerifierRegistrationTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory("plugwright-certificates-").FullName;

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    [Fact]
    public void Verifier_that_trusts_nothing_is_rejected()
    {
        var builder = TestHost.CreateBuilder();

        var exception = Should.Throw<ArgumentException>(() => builder.PluginVerifiers.AddCertificateVerifier(_ => { }));

        exception.Message.ShouldContain("trusts no certificate");
    }

    [Fact]
    public void Invalid_thumbprint_is_rejected_at_registration()
    {
        var builder = TestHost.CreateBuilder();

        Should.Throw<ArgumentException>(() => builder.PluginVerifiers.AddCertificateVerifier(options => options.TrustedThumbprints.Add("not a thumbprint")));
    }

    [Fact]
    public void Missing_issuer_certificate_is_rejected_at_registration()
    {
        var builder = TestHost.CreateBuilder();

        var exception = Should.Throw<InvalidOperationException>(() => builder.PluginVerifiers.AddCertificateVerifier(options =>
            options.TrustedPublishers.Add(new TrustedPublisher { IssuerCertificate = "missing-ca.crt", Subject = "CN=Acme" })));

        exception.Message.ShouldContain(Path.Combine(builder.Environment.ContentRootPath, "missing-ca.crt"));
    }

    [Fact]
    public void Settings_are_bound_from_a_configuration_section()
    {
        using var thumbprintCertificate = TestCertificates.CreateSelfSigned("CN=By Thumbprint");
        using var authority = TestCertificates.CreateAuthority("CN=Acme Root CA");
        using var publisherCertificate = TestCertificates.CreateIssued(authority, "CN=Acme Plugins, O=Acme");
        var issuerFile = Path.Combine(_directory, "acme-ca.crt");
        File.WriteAllText(issuerFile, authority.ExportCertificatePem());

        var builder = TestHost.CreateBuilder(new Dictionary<string, string?>
        {
            ["Verification:TrustedThumbprints:0"] = thumbprintCertificate.GetCertHashString(HashAlgorithmName.SHA256),
            ["Verification:TrustedPublishers:0:IssuerCertificate"] = issuerFile,
            ["Verification:TrustedPublishers:0:Subject"] = "CN=Acme Plugins, O=Acme",
            ["Verification:RevocationMode"] = "NoCheck",
        });

        builder.PluginVerifiers.AddCertificateVerifier(builder.Configuration.GetSection("Verification"));

        var verifier = builder.PluginVerifiers.Single();
        verifier.Verify(Request(thumbprintCertificate)).Message.ShouldContain("trusted by thumbprint");
        verifier.Verify(Request(publisherCertificate)).Message.ShouldContain("trusted as publisher");
    }

    [Fact]
    public void Revocation_mode_defaults_to_NoCheck()
    {
        new CertificateVerifierOptions().RevocationMode.ShouldBe(X509RevocationMode.NoCheck);
    }

    private static PluginVerificationRequest Request(X509Certificate2 signer)
    {
        (string Path, string Content)[] files = [("Acme.Greeter.dll", "main")];
        var manifest = TestCertificates.SignManifest(signer, "Acme.Greeter", files);
        return new PluginVerificationRequest(
            new PluginDirectorySource("plugins"),
            "Acme.Greeter",
            "/plugins/Acme.Greeter",
            [new PluginFile("Acme.Greeter.dll", Encoding.UTF8.GetBytes("main")), new PluginFile(PluginManifest.FileName, manifest)]);
    }
}
