using Microsoft.Extensions.Configuration;
using NuGet.Packaging;
using Plugwright.NuGet;

namespace Plugwright;

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
            section.Bind(options, binder => binder.ErrorOnUnknownConfiguration = true);
            return verifiers.Add(new PackageSignatureVerifier(CertificateTrust.Create(options, verifiers.ContentRoot)));
        }
    }
}
