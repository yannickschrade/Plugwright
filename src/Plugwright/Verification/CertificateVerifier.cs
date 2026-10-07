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
    private readonly CertificateTrust _trust;

    internal CertificateVerifier(CertificateTrust trust)
    {
        _trust = trust;
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
            if (!_trust.IsTrusted(certificate, certificates, out trust))
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
}
