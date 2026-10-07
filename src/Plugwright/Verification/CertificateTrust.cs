using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Plugwright;

/// <summary>
/// Which signing certificates are trusted: by thumbprint, or as publisher (subject plus issuing certificate authority).
/// Shared by the <see cref="CertificateVerifier"/> and the package signature verifier of Plugwright.NuGet.
/// </summary>
internal sealed class CertificateTrust
{
    private readonly HashSet<string> _trustedThumbprints;
    private readonly List<(X509Certificate2 Issuer, string Subject, string NormalizedSubject)> _trustedPublishers;
    private readonly X509RevocationMode _revocationMode;

    public CertificateTrust(
        IEnumerable<string> trustedThumbprints,
        IEnumerable<(X509Certificate2 Issuer, string Subject)> trustedPublishers,
        X509RevocationMode revocationMode)
    {
        _trustedThumbprints = trustedThumbprints.Select(NormalizeThumbprint).ToHashSet(StringComparer.Ordinal);
        _trustedPublishers = [.. trustedPublishers.Select(publisher => (publisher.Issuer, publisher.Subject, NormalizeName(publisher.Subject)))];
        _revocationMode = revocationMode;
    }

    /// <summary>
    /// Creates the trust from verifier options. Issuer certificates are loaded from files relative to the content root.
    /// </summary>
    /// <exception cref="ArgumentException">The options trust no certificate, or a setting is invalid.</exception>
    /// <exception cref="InvalidOperationException">The issuer certificate of a trusted publisher cannot be loaded.</exception>
    public static CertificateTrust Create(CertificateVerifierOptions options, string contentRoot)
    {
        if (options.TrustedThumbprints.Count == 0 && options.TrustedPublishers.Count == 0)
        {
            throw new ArgumentException(
                "The verifier trusts no certificate. Add at least one trusted thumbprint or trusted publisher.", nameof(options));
        }

        List<(X509Certificate2 Issuer, string Subject)> publishers = [];
        foreach (var publisher in options.TrustedPublishers)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(publisher.IssuerCertificate, nameof(publisher.IssuerCertificate));
            ArgumentException.ThrowIfNullOrWhiteSpace(publisher.Subject, nameof(publisher.Subject));

            var path = Path.GetFullPath(publisher.IssuerCertificate, contentRoot);
            try
            {
                publishers.Add((X509CertificateLoader.LoadCertificateFromFile(path), publisher.Subject));
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or CryptographicException)
            {
                throw new InvalidOperationException($"The issuer certificate '{path}' of a trusted publisher cannot be loaded: {exception.Message}", exception);
            }
        }

        return new CertificateTrust(options.TrustedThumbprints, publishers, options.RevocationMode);
    }

    /// <summary>
    /// Checks whether a signing certificate is currently valid and trusted.
    /// </summary>
    /// <param name="certificate">The signing certificate.</param>
    /// <param name="certificates">Further certificates from the signature, used to build the chain.</param>
    /// <param name="trustOrProblem">How the certificate is trusted, or why not.</param>
    public bool IsTrusted(X509Certificate2 certificate, X509Certificate2Collection certificates, out string trustOrProblem)
    {
        var now = DateTime.Now;
        if (now < certificate.NotBefore || now > certificate.NotAfter)
        {
            trustOrProblem = $"The signing certificate '{certificate.Subject}' is not valid now. "
                + $"It is valid from {certificate.NotBefore:u} to {certificate.NotAfter:u}.";
            return false;
        }

        var sha256Thumbprint = certificate.GetCertHashString(HashAlgorithmName.SHA256);
        if (_trustedThumbprints.Contains(sha256Thumbprint) || _trustedThumbprints.Contains(certificate.Thumbprint))
        {
            trustOrProblem = $"trusted by thumbprint {sha256Thumbprint}";
            return true;
        }

        var subject = NormalizeName(certificate.Subject);
        string? chainProblem = null;
        foreach (var (issuer, trustedSubject, _) in _trustedPublishers.Where(publisher => publisher.NormalizedSubject == subject))
        {
            using var chain = new X509Chain();
            chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
            chain.ChainPolicy.CustomTrustStore.Add(issuer);
            chain.ChainPolicy.ExtraStore.AddRange(certificates);
            chain.ChainPolicy.RevocationMode = _revocationMode;
            chain.ChainPolicy.RevocationFlag = X509RevocationFlag.ExcludeRoot;

            if (chain.Build(certificate))
            {
                trustOrProblem = $"trusted as publisher '{trustedSubject}' issued by '{issuer.Subject}'";
                return true;
            }

            chainProblem = string.Join(" ", chain.ChainStatus.Select(status => status.StatusInformation.Trim()).Distinct());
        }

        trustOrProblem = chainProblem is null
            ? $"The signing certificate '{certificate.Subject}' (thumbprint {sha256Thumbprint}) is not trusted."
            : $"The signing certificate '{certificate.Subject}' matches a trusted publisher, but its chain is not valid: {chainProblem}";
        return false;
    }

    public static string NormalizeThumbprint(string thumbprint)
    {
        var normalized = thumbprint.Replace(" ", "", StringComparison.Ordinal).Replace(":", "", StringComparison.Ordinal).ToUpperInvariant();
        if (normalized.Length is not (40 or 64) || !normalized.All(char.IsAsciiHexDigit))
        {
            throw new ArgumentException($"'{thumbprint}' is not a SHA-1 or SHA-256 certificate thumbprint.", nameof(thumbprint));
        }

        return normalized;
    }

    // Distinguished names are compared in .NET's canonical form, so that spacing in the configuration does not matter.
    private static string NormalizeName(string distinguishedName) =>
        new X500DistinguishedName(distinguishedName).Format(multiLine: false).ToUpperInvariant();
}
