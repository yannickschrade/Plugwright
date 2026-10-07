using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using NullLogger = NuGet.Common.NullLogger;
using NuGet.Packaging.Signing;

// For the samples only. Never use these certificates outside the samples.
//
// SampleCertificates <output-directory>
//   Writes acme-ca.crt (the CA certificate the host trusts) and acme-plugins.pfx (the signing certificate with its
//   private key and the CA certificate, password "sample"). Existing files are kept, so that the certificates stay
//   the same between builds. Used by the SignedPlugins and SignedPackages samples.
//
// SampleCertificates <output-directory> sign-packages <package-directory>
//   Signs every .nupkg in <package-directory> with acme-packages.pfx, a self-signed certificate (acme-packages.crt).
//   NuGet only accepts certificates that chain to a root the signing machine trusts, or self-signed ones; the sample
//   CA is neither. Publishers usually sign with 'dotnet nuget sign' and a certificate from a public CA.
var output = args[0];
var caFile = Path.Combine(output, "acme-ca.crt");
var pfxFile = Path.Combine(output, "acme-plugins.pfx");
var packageCertificateFile = Path.Combine(output, "acme-packages.crt");
var packagePfxFile = Path.Combine(output, "acme-packages.pfx");

if (!File.Exists(caFile) || !File.Exists(pfxFile))
{
    CreateCertificates(caFile, pfxFile);
}

if (args.Length == 3 && args[1] == "sign-packages")
{
    if (!File.Exists(packageCertificateFile) || !File.Exists(packagePfxFile))
    {
        CreatePackageCertificate(packageCertificateFile, packagePfxFile);
    }

    SignPackages(args[2], packagePfxFile);
}

static void CreateCertificates(string caFile, string pfxFile)
{
    Directory.CreateDirectory(Path.GetDirectoryName(caFile)!);

    // RSA with the code signing purpose, because NuGet package signing requires both.
    using var caKey = RSA.Create(2048);
    var caRequest = new CertificateRequest("CN=Acme Sample Root CA, O=Acme", caKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
    caRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
    caRequest.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign, true));
    caRequest.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(caRequest.PublicKey, false));
    using var ca = caRequest.CreateSelfSigned(DateTimeOffset.Now.AddDays(-1), DateTimeOffset.Now.AddYears(5));

    using var signingKey = RSA.Create(2048);
    var signingRequest = new CertificateRequest("CN=Acme Plugins, O=Acme", signingKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
    signingRequest.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, true));
    signingRequest.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension([new Oid("1.3.6.1.5.5.7.3.3")], false));
    using var signingCertificate = signingRequest
        .Create(ca, DateTimeOffset.Now.AddDays(-1), DateTimeOffset.Now.AddYears(1), RandomNumberGenerator.GetBytes(8))
        .CopyWithPrivateKey(signingKey);

    File.WriteAllText(caFile, ca.ExportCertificatePem());

    // The PFX file also contains the CA certificate, so that signing tools can build the chain.
    var pfx = new X509Certificate2Collection { signingCertificate, X509CertificateLoader.LoadCertificate(ca.RawData) };
    File.WriteAllBytes(pfxFile, pfx.Export(X509ContentType.Pkcs12, "sample")!);
    Console.WriteLine($"Created {caFile} and {pfxFile}.");
}

static void CreatePackageCertificate(string certificateFile, string pfxFile)
{
    using var key = RSA.Create(2048);
    var request = new CertificateRequest("CN=Acme Packages, O=Acme", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
    request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, true));
    request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension([new Oid("1.3.6.1.5.5.7.3.3")], false));
    using var certificate = request.CreateSelfSigned(DateTimeOffset.Now.AddDays(-1), DateTimeOffset.Now.AddYears(1));

    File.WriteAllText(certificateFile, certificate.ExportCertificatePem());
    File.WriteAllBytes(pfxFile, certificate.Export(X509ContentType.Pkcs12, "sample"));
    Console.WriteLine($"Created {certificateFile} and {pfxFile}.");
}

static void SignPackages(string packageDirectory, string pfxFile)
{
    foreach (var package in Directory.GetFiles(packageDirectory, "*.nupkg"))
    {
        var signingCertificate = X509CertificateLoader.LoadPkcs12FromFile(pfxFile, "sample");
        var signed = package + ".signed";

        using (var options = SigningOptions.CreateFromFilePaths(package, signed, overwrite: true, new X509SignatureProvider(timestampProvider: null), NullLogger.Instance))
        using (var request = new AuthorSignPackageRequest(signingCertificate, NuGet.Common.HashAlgorithmName.SHA256))
        {
            SigningUtility.SignAsync(options, request, CancellationToken.None).GetAwaiter().GetResult();
        }

        File.Move(signed, package, overwrite: true);
        Console.WriteLine($"Signed {Path.GetFileName(package)}.");
    }
}
