using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace Plugwright.Tests;

public sealed class CertificateVerifierTests
{
    private const string Publisher = "CN=Acme Plugins, O=Acme";

    private static readonly (string Path, string Content)[] s_files = [("Acme.Greeter.dll", "main"), ("lib/Helper.dll", "helper")];

    [Fact]
    public void Plugin_signed_by_a_trusted_thumbprint_passes()
    {
        using var certificate = TestCertificates.CreateSelfSigned(Publisher);

        var result = Verify(ByThumbprint(certificate.GetCertHashString(HashAlgorithmName.SHA256)), Request(certificate));

        result.Succeeded.ShouldBeTrue(result.Message);
        result.Message.ShouldContain("trusted by thumbprint");
        result.Message.ShouldContain("all 2 files match");
    }

    [Fact]
    public void Thumbprints_may_be_sha1_lowercase_or_with_colons()
    {
        using var certificate = TestCertificates.CreateSelfSigned(Publisher);
        var sha1WithColons = string.Join(":", certificate.Thumbprint.Chunk(2).Select(pair => new string(pair))).ToLowerInvariant();

        Verify(ByThumbprint(sha1WithColons), Request(certificate)).Succeeded.ShouldBeTrue();
    }

    [Fact]
    public void Plugin_signed_by_another_certificate_fails()
    {
        using var trusted = TestCertificates.CreateSelfSigned(Publisher);
        using var other = TestCertificates.CreateSelfSigned(Publisher);

        var result = Verify(ByThumbprint(trusted.Thumbprint), Request(other));

        result.Succeeded.ShouldBeFalse();
        result.Message.ShouldContain("is not trusted");
    }

    [Fact]
    public void Expired_certificate_fails_even_if_trusted()
    {
        using var certificate = TestCertificates.CreateSelfSigned(Publisher, DateTimeOffset.Now.AddYears(-2), DateTimeOffset.Now.AddDays(-1));

        var result = Verify(ByThumbprint(certificate.Thumbprint), Request(certificate));

        result.Message.ShouldContain("is not valid now");
    }

    [Fact]
    public void Certificate_issued_to_a_trusted_publisher_passes()
    {
        using var authority = TestCertificates.CreateAuthority("CN=Acme Root CA");
        using var certificate = TestCertificates.CreateIssued(authority, Publisher);

        var result = Verify(ByPublisher(authority, "CN=Acme Plugins,O=Acme"), Request(certificate));

        result.Succeeded.ShouldBeTrue(result.Message);
        result.Message.ShouldContain("trusted as publisher");
    }

    [Fact]
    public void Intermediate_certificates_from_the_signature_complete_the_chain()
    {
        using var root = TestCertificates.CreateAuthority("CN=Acme Root CA");
        using var intermediate = TestCertificates.CreateAuthority("CN=Acme Signing CA", root);
        using var certificate = TestCertificates.CreateIssued(intermediate, Publisher);

        var withIntermediate = Request(certificate, extraCertificates: [intermediate]);
        var withoutIntermediate = Request(certificate);

        Verify(ByPublisher(root, Publisher), withIntermediate).Succeeded.ShouldBeTrue();
        Verify(ByPublisher(root, Publisher), withoutIntermediate).Succeeded.ShouldBeFalse();
    }

    [Fact]
    public void Certificate_with_another_subject_fails()
    {
        using var authority = TestCertificates.CreateAuthority("CN=Acme Root CA");
        using var certificate = TestCertificates.CreateIssued(authority, "CN=Somebody Else");

        Verify(ByPublisher(authority, Publisher), Request(certificate)).Message.ShouldContain("is not trusted");
    }

    [Fact]
    public void Certificate_from_another_authority_fails()
    {
        using var trustedAuthority = TestCertificates.CreateAuthority("CN=Acme Root CA");
        using var otherAuthority = TestCertificates.CreateAuthority("CN=Acme Root CA");
        using var certificate = TestCertificates.CreateIssued(otherAuthority, Publisher);

        var result = Verify(ByPublisher(trustedAuthority, Publisher), Request(certificate));

        result.Succeeded.ShouldBeFalse();
        result.Message.ShouldContain("its chain is not valid");
    }

    [Fact]
    public void Revocation_check_fails_when_the_status_is_unknown()
    {
        using var authority = TestCertificates.CreateAuthority("CN=Acme Root CA");
        using var certificate = TestCertificates.CreateIssued(authority, Publisher);

        var result = Verify(ByPublisher(authority, Publisher, X509RevocationMode.Offline), Request(certificate));

        result.Succeeded.ShouldBeFalse();
        result.Message.ShouldContain("its chain is not valid");
    }

    [Fact]
    public void Unsigned_plugin_fails()
    {
        using var certificate = TestCertificates.CreateSelfSigned(Publisher);

        var result = Verify(ByThumbprint(certificate.Thumbprint), Request("Acme.Greeter", manifest: null, s_files));

        result.Message.ShouldBe("The plugin is not signed: 'plugwright.manifest.p7s' is missing.");
    }

    [Fact]
    public void Corrupt_manifest_fails_wherever_it_is_damaged()
    {
        using var certificate = TestCertificates.CreateSelfSigned(Publisher);
        var manifest = TestCertificates.SignManifest(certificate, "Acme.Greeter", s_files);

        var original = Verify(ByThumbprint(certificate.Thumbprint), Request("Acme.Greeter", manifest, s_files));

        // Flip one byte at a time: in the content, the signature, the embedded certificate or the structure.
        // Damage must never throw. A few bytes are not covered by the signature, for example the list of digest
        // algorithms, which is only a hint; damage there may pass, but must not change what was verified.
        for (var position = 0; position < manifest.Length; position++)
        {
            var damaged = manifest.ToArray();
            damaged[position] ^= 0xFF;

            var result = Verify(ByThumbprint(certificate.Thumbprint), Request("Acme.Greeter", damaged, s_files));

            if (result.Succeeded)
            {
                result.Message.ShouldBe(original.Message, $"Damage at byte {position} changed what was verified.");
            }
        }
    }

    [Fact]
    public void Manifest_for_another_plugin_fails()
    {
        using var certificate = TestCertificates.CreateSelfSigned(Publisher);
        var manifest = TestCertificates.SignManifest(certificate, "Acme.Other", s_files);

        var result = Verify(ByThumbprint(certificate.Thumbprint), Request("Acme.Greeter", manifest, s_files));

        result.Message.ShouldBe("The manifest is signed for plugin 'Acme.Other', but the plugin is installed as 'Acme.Greeter'.");
    }

    [Theory]
    [InlineData("changed", "File 'Acme.Greeter.dll' does not match its hash in the signed manifest.")]
    [InlineData("added", "File 'Evil.dll' is not listed in the signed manifest.")]
    [InlineData("missing", "File 'lib/Helper.dll' is listed in the signed manifest but missing.")]
    public void Files_must_match_the_manifest_exactly(string change, string expectedMessage)
    {
        using var certificate = TestCertificates.CreateSelfSigned(Publisher);
        var manifest = TestCertificates.SignManifest(certificate, "Acme.Greeter", s_files);
        (string, string)[] files = change switch
        {
            "changed" => [("Acme.Greeter.dll", "changed"), ("lib/Helper.dll", "helper")],
            "added" => [.. s_files, ("Evil.dll", "evil")],
            _ => [("Acme.Greeter.dll", "main")],
        };

        Verify(ByThumbprint(certificate.Thumbprint), Request("Acme.Greeter", manifest, files)).Message.ShouldBe(expectedMessage);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("XYZ0000000000000000000000000000000000000")]
    public void Invalid_thumbprints_are_rejected(string thumbprint)
    {
        Should.Throw<ArgumentException>(() => CertificateVerifier.NormalizeThumbprint(thumbprint));
    }

    private static PluginVerificationResult Verify(CertificateVerifier verifier, PluginVerificationRequest request) => verifier.Verify(request);

    private static CertificateVerifier ByThumbprint(string thumbprint) => new([thumbprint], [], X509RevocationMode.NoCheck);

    private static CertificateVerifier ByPublisher(X509Certificate2 issuer, string subject, X509RevocationMode revocationMode = X509RevocationMode.NoCheck) =>
        new([], [(X509CertificateLoader.LoadCertificate(issuer.RawData), subject)], revocationMode);

    private static PluginVerificationRequest Request(X509Certificate2 signer, IEnumerable<X509Certificate2>? extraCertificates = null) =>
        Request("Acme.Greeter", TestCertificates.SignManifest(signer, "Acme.Greeter", s_files, extraCertificates), s_files);

    private static PluginVerificationRequest Request(string name, byte[]? manifest, IEnumerable<(string Path, string Content)> files)
    {
        List<PluginFile> pluginFiles = [.. files.Select(file => new PluginFile(file.Path, Encoding.UTF8.GetBytes(file.Content)))];
        if (manifest is not null)
        {
            pluginFiles.Add(new PluginFile(PluginManifest.FileName, manifest));
        }

        return new PluginVerificationRequest(new PluginDirectorySource("plugins"), name, $"/plugins/{name}", [.. pluginFiles.OrderBy(file => file.Path, StringComparer.Ordinal)]);
    }
}
