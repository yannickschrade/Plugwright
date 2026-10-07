using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

// Usage: SampleCertificates <output-directory>
// Writes acme-ca.crt (the CA certificate the host trusts) and acme-plugins.pfx (the signing certificate with its
// private key, password "sample"). Existing files are kept, so that the certificates stay the same between builds.
// Never use these certificates outside the sample.
var output = args[0];
var caFile = Path.Combine(output, "acme-ca.crt");
var pfxFile = Path.Combine(output, "acme-plugins.pfx");
if (File.Exists(caFile) && File.Exists(pfxFile))
{
    return;
}

Directory.CreateDirectory(output);

using var caKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
var caRequest = new CertificateRequest("CN=Acme Sample Root CA, O=Acme", caKey, HashAlgorithmName.SHA256);
caRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
caRequest.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign, true));
caRequest.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(caRequest.PublicKey, false));
using var ca = caRequest.CreateSelfSigned(DateTimeOffset.Now.AddDays(-1), DateTimeOffset.Now.AddYears(5));

using var signingKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
var signingRequest = new CertificateRequest("CN=Acme Plugins, O=Acme", signingKey, HashAlgorithmName.SHA256);
signingRequest.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, true));
using var signingCertificate = signingRequest
    .Create(ca, DateTimeOffset.Now.AddDays(-1), DateTimeOffset.Now.AddYears(1), RandomNumberGenerator.GetBytes(8))
    .CopyWithPrivateKey(signingKey);

File.WriteAllText(caFile, ca.ExportCertificatePem());
File.WriteAllBytes(pfxFile, signingCertificate.Export(X509ContentType.Pkcs12, "sample"));
Console.WriteLine($"Created {caFile} and {pfxFile}.");
