using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Plugwright.IntegrationTests;

/// <summary>
/// Creates certificates and PFX files for signing test plugins.
/// </summary>
internal static class TestCertificates
{
    public const string Password = "test-password";

    public static X509Certificate2 CreateSelfSigned(string subject)
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        return new CertificateRequest(subject, key, HashAlgorithmName.SHA256)
            .CreateSelfSigned(DateTimeOffset.Now.AddDays(-1), DateTimeOffset.Now.AddYears(1));
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
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, critical: true));

        using var certificate = request.Create(issuer, DateTimeOffset.Now.AddDays(-1), DateTimeOffset.Now.AddYears(1), RandomNumberGenerator.GetBytes(8));
        return certificate.CopyWithPrivateKey(key);
    }

    /// <summary>
    /// Writes a PFX file with the signing certificate (with private key) and further certificates of its chain.
    /// </summary>
    public static string WritePfx(string path, X509Certificate2 signingCertificate, params X509Certificate2[] chain)
    {
        var collection = new X509Certificate2Collection(signingCertificate);
        foreach (var certificate in chain)
        {
            collection.Add(X509CertificateLoader.LoadCertificate(certificate.RawData));
        }

        File.WriteAllBytes(path, collection.Export(X509ContentType.Pkcs12, Password)!);
        return path;
    }
}
