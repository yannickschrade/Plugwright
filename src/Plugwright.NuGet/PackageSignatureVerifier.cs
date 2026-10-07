using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;
using NuGet.Packaging;
using NuGet.Packaging.Signing;

namespace Plugwright;

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

/// <summary>
/// Adds the <see cref="PackageSignatureVerifier"/> to <c>builder.PluginVerifiers</c>.
/// </summary>
public static class PackageVerifierExtensions
{
    extension(PluginVerifierCollection verifiers)
    {
        /// <summary>
        /// Adds the <see cref="PackageSignatureVerifier"/>, which verifies the NuGet signature of every package.
        /// </summary>
        /// <param name="configure">Configures which certificates are trusted. At least one thumbprint or publisher is required.</param>
        /// <returns>The collection, for chaining.</returns>
        /// <exception cref="ArgumentException">The settings trust no certificate, or a setting is invalid.</exception>
        /// <exception cref="InvalidOperationException">The issuer certificate of a trusted publisher cannot be loaded.</exception>
        /// <example>
        /// <code language="csharp">
        /// builder.PluginVerifiers.AddPackageSignatureVerifier(options =&gt;
        ///     options.TrustedThumbprints.Add("9F2DB14A363EBF6A1DA40F4229766BD35E55261904EBD500AEE48B0C8869EB60"));
        /// </code>
        /// </example>
        public PluginVerifierCollection AddPackageSignatureVerifier(Action<CertificateVerifierOptions> configure)
        {
            ArgumentNullException.ThrowIfNull(verifiers);
            ArgumentNullException.ThrowIfNull(configure);
            var options = new CertificateVerifierOptions();
            configure(options);
            return verifiers.Add(new PackageSignatureVerifier(CertificateTrust.Create(options, verifiers.ContentRoot)));
        }

        /// <summary>
        /// Adds the <see cref="PackageSignatureVerifier"/> with settings bound from a configuration section.
        /// </summary>
        /// <remarks>
        /// The settings are the basis of trust. If they come from <c>appsettings.json</c>, protect that file
        /// from whoever can write to the package folder.
        /// </remarks>
        /// <param name="section">The section with the settings, for example <c>Plugwright:Verification:PackageSignature</c>.</param>
        /// <returns>The collection, for chaining.</returns>
        public PluginVerifierCollection AddPackageSignatureVerifier(IConfigurationSection section)
        {
            ArgumentNullException.ThrowIfNull(verifiers);
            ArgumentNullException.ThrowIfNull(section);
            var options = new CertificateVerifierOptions();
            section.Bind(options);
            return verifiers.Add(new PackageSignatureVerifier(CertificateTrust.Create(options, verifiers.ContentRoot)));
        }
    }
}
