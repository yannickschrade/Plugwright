using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Plugwright;

/// <summary>
/// Verifies plugin folders by their signed manifest: the plugin's publisher signs a list of all plugin files,
/// and the host trusts the publisher's certificate.
/// </summary>
/// <remarks>
/// <para>
/// Every plugin folder must contain the file <c>plugwright.manifest.p7s</c>, created with <c>plugwright sign</c>. It is a
/// CMS signature over the plugin ID, the plugin version and the sha256 hash of every other file in the folder.
/// A plugin passes when the signature is valid, the signing certificate is trusted and currently valid, the manifest
/// is for the plugin installed in this folder, and the folder contains exactly the files listed, unchanged.
/// </para>
/// <para>
/// Unlike a lock file, the manifest needs no update on the host when a plugin is updated, as long as the same publisher
/// signs it. There is no timestamping: when the signing certificate expires, plugins must be signed again.
/// </para>
/// <para>
/// Register it with <see cref="PluginVerifierCollection.AddCertificateVerifier(Action{CertificateVerifierOptions})"/>.
/// </para>
/// </remarks>
public sealed class CertificateVerifier : IPluginVerifier
{
    private readonly HashSet<string> _trustedThumbprints;
    private readonly List<(X509Certificate2 Issuer, string Subject, string NormalizedSubject)> _trustedPublishers;
    private readonly X509RevocationMode _revocationMode;

    internal CertificateVerifier(
        IEnumerable<string> trustedThumbprints,
        IEnumerable<(X509Certificate2 Issuer, string Subject)> trustedPublishers,
        X509RevocationMode revocationMode)
    {
        _trustedThumbprints = trustedThumbprints.Select(NormalizeThumbprint).ToHashSet(StringComparer.Ordinal);
        _trustedPublishers = [.. trustedPublishers.Select(publisher => (publisher.Issuer, publisher.Subject, NormalizeName(publisher.Subject)))];
        _revocationMode = revocationMode;
    }

    /// <summary>
    /// Returns <see langword="true"/> for plugin directories.
    /// </summary>
    /// <param name="source">The source the plugin comes from.</param>
    /// <returns>Whether the source is a <see cref="PluginDirectorySource"/>.</returns>
    public bool CanVerify(PluginSource source) => source is PluginDirectorySource;

    /// <summary>
    /// Checks the plugin's signed manifest and compares its files with the manifest.
    /// </summary>
    /// <param name="request">The plugin to verify.</param>
    /// <returns>Success if the manifest is signed by a trusted certificate and all files match it exactly.</returns>
    public PluginVerificationResult Verify(PluginVerificationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var manifestFile = request.Files.FirstOrDefault(file => file.Path == PluginManifest.FileName);
        if (manifestFile is null)
        {
            return PluginVerificationResult.Failure($"The plugin is not signed: '{PluginManifest.FileName}' is missing.");
        }

        PluginManifest manifest;
        X509Certificate2? certificate;
        X509Certificate2Collection certificates;
        try
        {
            (manifest, var signer, certificates) = PluginManifest.Decode(manifestFile.Content.Span);
            certificate = signer.Certificate;
        }
        catch (Exception exception) when (exception is CryptographicException or InvalidDataException)
        {
            return PluginVerificationResult.Failure($"The manifest is invalid: {exception.Message}");
        }

        if (certificate is null)
        {
            return PluginVerificationResult.Failure("The manifest does not contain the signing certificate.");
        }

        string trust;
        try
        {
            if (!IsTrusted(certificate, certificates, out trust))
            {
                return PluginVerificationResult.Failure(trust);
            }
        }
        catch (CryptographicException exception)
        {
            return PluginVerificationResult.Failure($"The signing certificate in the manifest is invalid: {exception.Message}");
        }

        if (!string.Equals(manifest.PluginId, request.Name, StringComparison.OrdinalIgnoreCase))
        {
            return PluginVerificationResult.Failure(
                $"The manifest is signed for plugin '{manifest.PluginId}', but the plugin is installed as '{request.Name}'.");
        }

        foreach (var file in request.Files.Where(file => file != manifestFile))
        {
            if (!manifest.Files.TryGetValue(file.Path, out var expectedHash))
            {
                return PluginVerificationResult.Failure($"File '{file.Path}' is not listed in the signed manifest.");
            }

            if (PluginFileHash.Check(file.Path, expectedHash, file.Content.Span, "the signed manifest") is { } problem)
            {
                return PluginVerificationResult.Failure(problem);
            }
        }

        var missing = manifest.Files.Keys.Except(request.Files.Select(file => file.Path), StringComparer.Ordinal).Order(StringComparer.Ordinal).FirstOrDefault();
        if (missing is not null)
        {
            return PluginVerificationResult.Failure($"File '{missing}' is listed in the signed manifest but missing.");
        }

        return PluginVerificationResult.Success(
            $"Manifest for {manifest.PluginId} {manifest.PluginVersion} signed by '{certificate.Subject}', {trust}; "
            + $"all {manifest.Files.Count} files match.");
    }

    private bool IsTrusted(X509Certificate2 certificate, X509Certificate2Collection certificates, out string trustOrProblem)
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

    internal static string NormalizeThumbprint(string thumbprint)
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
