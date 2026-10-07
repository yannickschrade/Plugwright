using System.IO.Compression;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NullLogger = NuGet.Common.NullLogger;
using NuGet.Packaging.Signing;
using Plugwright.Cli;

namespace Plugwright.IntegrationTests;

[Collection(LoadContextTests.Name)]
public sealed class PackageVerificationTests(TestPluginsFixture testPlugins) : IDisposable
{
    private const string Greeter = "Plugwright.TestPackages.Greeter.1.0.0";
    private const string LibraryV1 = "Plugwright.TestPackages.Library.1.0.0";
    private const string GreeterId = "Plugwright.TestPackages.Greeter";

    private readonly TempDirectory _directory = new();

    private string PackageDirectory => Path.Combine(_directory.Path, "packages");

    private string LockFile => Path.Combine(_directory.Path, "plugins.lock.json");

    public void Dispose() => _directory.Dispose();

    [Fact]
    public void Lock_file_covers_plugin_and_library_packages()
    {
        Install(Greeter);
        Install(LibraryV1);
        CreateLockFile();

        var report = Load(builder => builder.PluginVerifiers.AddHashVerifier());

        report.Verifications.Select(verification => (verification.PluginName, verification.Succeeded))
            .ShouldBe([(Greeter, true), (LibraryV1, true)]);
        report.Plugins.ShouldHaveSingleItem().Metadata.Id.ShouldBe(GreeterId);
    }

    [Fact]
    public void Changed_library_package_fails_verification()
    {
        Install(Greeter);
        var library = Install(LibraryV1);
        CreateLockFile();
        File.AppendAllText(library, "changed");

        var exception = Should.Throw<PluginException>(() => Load(builder => builder.PluginVerifiers.AddHashVerifier()));

        exception.Message.ShouldContain($"Verification by HashVerifier failed: File '{LibraryV1}.nupkg' does not match its hash in the lock file.");
    }

    [Fact]
    public void Plugin_depending_on_a_rejected_library_is_skipped_with_SkipAndLog()
    {
        Install(Greeter);
        var library = Install(LibraryV1);
        CreateLockFile();
        File.AppendAllText(library, "changed");

        var report = Load(builder =>
        {
            builder.Plugins.Options.FailureMode = PluginFailureMode.SkipAndLog;
            builder.PluginVerifiers.AddHashVerifier();
        });

        report.Plugins.ShouldBeEmpty();
        report.SkippedPlugins.Select(skipped => skipped.PluginId).ShouldBe([LibraryV1, GreeterId]);
    }

    [Fact]
    public void Signed_packages_pass_with_a_trusted_thumbprint()
    {
        using var certificate = CreateCodeSigningCertificate();
        Sign(Install(Greeter), certificate);
        Sign(Install(LibraryV1), certificate);

        var report = Load(builder => builder.PluginVerifiers.AddPackageSignatureVerifier(options =>
            options.TrustedThumbprints.Add(certificate.GetCertHashString(HashAlgorithmName.SHA256))));

        report.Verifications.Count.ShouldBe(2);
        report.Verifications.ShouldAllBe(verification => verification.Succeeded && verification.Message.StartsWith("Author signature"));
        report.Plugins.ShouldHaveSingleItem();
    }

    [Fact]
    public void Unsigned_package_fails()
    {
        using var certificate = CreateCodeSigningCertificate();
        Install(Greeter);

        var exception = Should.Throw<PluginException>(() =>
            Load(builder => builder.PluginVerifiers.AddPackageSignatureVerifier(options => options.TrustedThumbprints.Add(certificate.Thumbprint))));

        exception.Message.ShouldContain($"The package '{Greeter}.nupkg' is not signed.");
    }

    [Fact]
    public void Package_signed_by_an_untrusted_certificate_fails()
    {
        using var trusted = CreateCodeSigningCertificate();
        using var other = CreateCodeSigningCertificate();
        Sign(Install(Greeter), other);

        var exception = Should.Throw<PluginException>(() =>
            Load(builder => builder.PluginVerifiers.AddPackageSignatureVerifier(options => options.TrustedThumbprints.Add(trusted.Thumbprint))));

        exception.Message.ShouldContain("is not trusted");
    }

    [Fact]
    public void Package_changed_after_signing_fails()
    {
        using var certificate = CreateCodeSigningCertificate();
        var package = Install(Greeter);
        Sign(package, certificate);
        using (var archive = ZipFile.Open(package, ZipArchiveMode.Update))
        {
            archive.CreateEntry("lib/net10.0/Extra.dll").Open().Dispose();
        }

        var exception = Should.Throw<PluginException>(() =>
            Load(builder => builder.PluginVerifiers.AddPackageSignatureVerifier(options => options.TrustedThumbprints.Add(certificate.Thumbprint))));

        exception.Message.ShouldContain("is invalid");
    }

    [Fact]
    public void Certificate_verifier_alone_cannot_verify_packages()
    {
        using var certificate = CreateCodeSigningCertificate();
        Install(Greeter);

        var exception = Should.Throw<PluginException>(() =>
            Load(builder => builder.PluginVerifiers.AddCertificateVerifier(options => options.TrustedThumbprints.Add(certificate.Thumbprint))));

        exception.Message.ShouldContain("No registered plugin verifier can verify plugins from this source (Packages ");
    }

    private string Install(string package) => testPlugins.InstallPackage(package, PackageDirectory);

    private void CreateLockFile()
    {
        using var output = new StringWriter();
        CliApp.Run(["lock", PackageDirectory, "--output", LockFile], output, output).ShouldBe(CliApp.Success, output.ToString());
    }

    private PluginReport Load(Action<HostApplicationBuilder> configure)
    {
        var builder = TestHost.CreateBuilder(_directory.Path);
        configure(builder);
        builder.Plugins.AddFromPackages("packages");
        builder.ApplyPlugins();
        var app = builder.Build();
        app.UsePlugins();
        return app.Services.GetRequiredService<PluginReport>();
    }

    /// <summary>
    /// NuGet signs packages with RSA certificates for code signing only.
    /// </summary>
    private static X509Certificate2 CreateCodeSigningCertificate()
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest("CN=Plugwright Test Packages", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension([new Oid("1.3.6.1.5.5.7.3.3")], critical: false));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, critical: true));
        return request.CreateSelfSigned(DateTimeOffset.Now.AddDays(-1), DateTimeOffset.Now.AddYears(1));
    }

    private static void Sign(string package, X509Certificate2 certificate)
    {
        // The sign request disposes the certificate it gets, so it gets a copy.
        using var copy = X509CertificateLoader.LoadPkcs12(certificate.Export(X509ContentType.Pkcs12), password: null);
        var signed = package + ".signed";
        using (var options = SigningOptions.CreateFromFilePaths(package, signed, overwrite: true, new X509SignatureProvider(timestampProvider: null), NullLogger.Instance))
        using (var request = new AuthorSignPackageRequest(copy, NuGet.Common.HashAlgorithmName.SHA256))
        {
            SigningUtility.SignAsync(options, request, CancellationToken.None).GetAwaiter().GetResult();
        }

        File.Move(signed, package, overwrite: true);
    }
}
