using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace Plugwright.Tests;

/// <summary>
/// Creates certificates and signed manifests for tests, without using Plugwright's own signing code.
/// </summary>
internal static class TestCertificates
{
    public static X509Certificate2 CreateSelfSigned(string subject, DateTimeOffset? notBefore = null, DateTimeOffset? notAfter = null)
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var request = new CertificateRequest(subject, key, HashAlgorithmName.SHA256);
        return request.CreateSelfSigned(notBefore ?? DateTimeOffset.Now.AddDays(-1), notAfter ?? DateTimeOffset.Now.AddYears(1));
    }

    public static X509Certificate2 CreateAuthority(string subject, X509Certificate2? issuer = null)
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var request = new CertificateRequest(subject, key, HashAlgorithmName.SHA256);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(certificateAuthority: true, hasPathLengthConstraint: false, 0, critical: true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign, critical: true));
        request.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(request.PublicKey, critical: false));

        if (issuer is null)
        {
            return request.CreateSelfSigned(DateTimeOffset.Now.AddDays(-1), DateTimeOffset.Now.AddYears(5));
        }

        using var certificate = request.Create(issuer, DateTimeOffset.Now.AddDays(-1), DateTimeOffset.Now.AddYears(4), RandomNumberGenerator.GetBytes(8));
        return certificate.CopyWithPrivateKey(key);
    }

    public static X509Certificate2 CreateIssued(X509Certificate2 issuer, string subject)
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var request = new CertificateRequest(subject, key, HashAlgorithmName.SHA256);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(certificateAuthority: false, hasPathLengthConstraint: false, 0, critical: true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, critical: true));

        using var certificate = request.Create(issuer, DateTimeOffset.Now.AddDays(-1), DateTimeOffset.Now.AddYears(1), RandomNumberGenerator.GetBytes(8));
        return certificate.CopyWithPrivateKey(key);
    }

    /// <summary>
    /// Creates the content of a manifest file: a CMS signature with the manifest JSON embedded.
    /// </summary>
    public static byte[] SignManifest(
        X509Certificate2 signer,
        string pluginId,
        IEnumerable<(string Path, string Content)> files,
        IEnumerable<X509Certificate2>? extraCertificates = null)
    {
        var entries = string.Join(", ", files.Select(file => $"\"{file.Path}\": \"{PluginFileHash.Compute(Encoding.UTF8.GetBytes(file.Content))}\""));
        var json = $$"""{ "version": 1, "pluginId": "{{pluginId}}", "pluginVersion": "1.0.0", "files": { {{entries}} } }""";

        var cmsSigner = new CmsSigner(signer) { IncludeOption = X509IncludeOption.EndCertOnly };
        cmsSigner.Certificates.AddRange((extraCertificates ?? []).ToArray());
        var signedCms = new SignedCms(new ContentInfo(Encoding.UTF8.GetBytes(json)), detached: false);
        signedCms.ComputeSignature(cmsSigner);
        return signedCms.Encode();
    }
}
