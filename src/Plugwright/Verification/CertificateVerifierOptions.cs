using System.Security.Cryptography.X509Certificates;

namespace Plugwright;

/// <summary>
/// Settings of the <see cref="CertificateVerifier"/>: which signers are trusted and how certificates are checked.
/// </summary>
/// <remarks>
/// A plugin is accepted if its manifest is signed by a certificate from <see cref="TrustedThumbprints"/> or by a
/// certificate that matches one of the <see cref="TrustedPublishers"/>. At least one of both must be set.
/// </remarks>
public sealed class CertificateVerifierOptions
{
    /// <summary>
    /// Gets the thumbprints of trusted signing certificates, as SHA-256 (64 hex digits) or SHA-1 (40 hex digits).
    /// Spaces and colons are ignored.
    /// </summary>
    /// <remarks>
    /// Trusting a thumbprint trusts exactly one certificate. When it is renewed, add the new thumbprint.
    /// </remarks>
    public IList<string> TrustedThumbprints { get; } = [];

    /// <summary>
    /// Gets the trusted publishers: certificates issued by a trusted certificate authority to a specific subject.
    /// </summary>
    /// <remarks>
    /// Trusting a publisher survives certificate renewal, as long as the new certificate has the same subject and is
    /// issued by the same authority.
    /// </remarks>
    public IList<TrustedPublisher> TrustedPublishers { get; } = [];

    /// <summary>
    /// Gets or sets whether the certificates of trusted publishers are checked for revocation.
    /// The default is <see cref="X509RevocationMode.NoCheck"/>, so that machines without network access can start.
    /// </summary>
    /// <remarks>
    /// <see cref="X509RevocationMode.Online"/> is recommended for <see cref="TrustedPublishers"/>: a stolen certificate
    /// that the authority has revoked is then rejected. If the revocation status cannot be determined, verification fails.
    /// Certificates trusted by thumbprint are not checked for revocation; remove the thumbprint instead.
    /// </remarks>
    public X509RevocationMode RevocationMode { get; set; } = X509RevocationMode.NoCheck;
}

/// <summary>
/// A trusted publisher: signing certificates with a given subject, issued by a given certificate authority.
/// </summary>
public sealed class TrustedPublisher
{
    /// <summary>
    /// Gets or sets the path of the certificate authority's certificate (PEM or DER). A relative path is resolved against
    /// the host's content root. Signing certificates must chain up to this certificate.
    /// </summary>
    public string IssuerCertificate { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the distinguished name the signing certificate must have, for example <c>CN=Acme Plugins, O=Acme</c>.
    /// </summary>
    public string Subject { get; set; } = string.Empty;
}
