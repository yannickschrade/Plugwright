using System.Security.Cryptography;
using NuGet.Packaging;
using NuGet.Packaging.Signing;

namespace Plugwright.NuGet;

/// <summary>
/// Verifies the NuGet signature of every package in a package folder, plugin packages and library packages alike.
/// </summary>
/// <remarks>
/// <para>
/// A package passes when it is signed, its signature is valid, its content matches the signature, and the signing
/// certificate is trusted and currently valid. The primary signature counts: the author signature of an author-signed
/// package, or the repository signature of a package that only a repository (such as nuget.org) signed.
/// </para>
/// <para>
/// Trust works as for the <see cref="CertificateVerifier"/>: by thumbprint or as publisher. Timestamps in the signature
/// are not used; the signing certificate must be valid at verification time.
/// </para>
/// <para>
/// Register it with <see cref="PackageVerifierExtensions.AddPackageSignatureVerifier(PluginVerifierCollection, Action{CertificateVerifierOptions})"/>.
/// Sign packages with <c>dotnet nuget sign</c>.
/// </para>
/// </remarks>
public sealed class PackageSignatureVerifier : IPluginVerifier
{
    private readonly CertificateTrust _trust;

    internal PackageSignatureVerifier(CertificateTrust trust)
    {
        _trust = trust;
    }

    /// <summary>
    /// Returns <see langword="true"/> for package folders.
    /// </summary>
    /// <param name="source">The source the plugin comes from.</param>
    /// <returns>Whether the source is a <see cref="PluginPackageSource"/>.</returns>
    public bool CanVerify(PluginSource source) => source is PluginPackageSource;

    /// <summary>
    /// Checks the package's signature, its integrity and the signing certificate.
    /// </summary>
    /// <param name="request">The package to verify; its only file is the .nupkg.</param>
    /// <returns>Success if the package is signed by a trusted certificate and unchanged.</returns>
    public PluginVerificationResult Verify(PluginVerificationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var packageFile = request.Files.Single();
        using var reader = new PackageArchiveReader(new MemoryStream(packageFile.Content.ToArray(), writable: false));

        PrimarySignature? signature;
        try
        {
            if (!reader.IsSignedAsync(CancellationToken.None).GetAwaiter().GetResult())
            {
                return PluginVerificationResult.Failure($"The package '{packageFile.Path}' is not signed.");
            }

            signature = reader.GetPrimarySignatureAsync(CancellationToken.None).GetAwaiter().GetResult();
            if (signature is null)
            {
                return PluginVerificationResult.Failure($"The package '{packageFile.Path}' is not signed.");
            }

            signature.SignedCms.CheckSignature(verifySignatureOnly: true);
            reader.ValidateIntegrityAsync(signature.SignatureContent, CancellationToken.None).GetAwaiter().GetResult();
        }
        catch (Exception exception) when (exception is SignatureException or CryptographicException or InvalidDataException)
        {
            return PluginVerificationResult.Failure($"The signature of the package '{packageFile.Path}' is invalid: {exception.Message}");
        }

        var certificate = signature.SignerInfo.Certificate;
        if (certificate is null)
        {
            return PluginVerificationResult.Failure($"The signature of the package '{packageFile.Path}' does not contain the signing certificate.");
        }

        string trust;
        try
        {
            if (!_trust.IsTrusted(certificate, signature.SignedCms.Certificates, out trust))
            {
                return PluginVerificationResult.Failure(trust);
            }
        }
        catch (CryptographicException exception)
        {
            return PluginVerificationResult.Failure($"The signing certificate of the package '{packageFile.Path}' is invalid: {exception.Message}");
        }

        return PluginVerificationResult.Success($"{signature.Type} signature by '{certificate.Subject}', {trust}; the package content matches.");
    }
}
