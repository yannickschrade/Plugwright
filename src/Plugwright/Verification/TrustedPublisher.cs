namespace Plugwright;

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
