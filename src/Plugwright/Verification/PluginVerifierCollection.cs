using System.Collections;
using Microsoft.Extensions.Configuration;

namespace Plugwright;

/// <summary>
/// The plugin verifiers of a host application, available as <c>builder.PluginVerifiers</c>.
/// </summary>
/// <remarks>
/// <para>
/// Verification is optional. Without verifiers, plugins are not verified and a notice is logged at startup.
/// With verifiers, every plugin from a directory must pass all verifiers responsible for it before it is loaded.
/// Plugins added in code are part of the host application and are not verified.
/// </para>
/// <para>
/// Which verifiers exist is always decided in code. Their settings come from code, unless you explicitly bind them
/// to a configuration section. Verifiers cannot be added after <c>builder.ApplyPlugins()</c>.
/// </para>
/// </remarks>
/// <example>
/// <code language="csharp">
/// builder.PluginVerifiers.AddHashVerifier(options => options.LockFile = "/etc/myapp/plugins.lock.json");
/// </code>
/// </example>
public sealed class PluginVerifierCollection : IReadOnlyCollection<IPluginVerifier>
{
    private readonly PluginSystem _system;
    private readonly string _contentRoot;
    private readonly List<IPluginVerifier> _verifiers = [];

    /// <summary>
    /// Gets the host's content root, for resolving relative paths in verifier settings.
    /// </summary>
    internal string ContentRoot => _contentRoot;

    internal PluginVerifierCollection(PluginSystem system, string contentRoot)
    {
        _system = system;
        _contentRoot = contentRoot;
    }

    /// <summary>
    /// Gets the number of registered verifiers.
    /// </summary>
    public int Count => _verifiers.Count;

    /// <summary>
    /// Adds a verifier.
    /// </summary>
    /// <param name="verifier">The verifier.</param>
    /// <returns>This collection, for chaining.</returns>
    /// <exception cref="InvalidOperationException"><c>builder.ApplyPlugins()</c> has already been called.</exception>
    /// <exception cref="ArgumentException">A verifier of the same type has already been added.</exception>
    public PluginVerifierCollection Add(IPluginVerifier verifier)
    {
        ArgumentNullException.ThrowIfNull(verifier);
        _system.ThrowIfApplied("Plugin verifiers cannot be added");

        if (_verifiers.Any(existing => existing.GetType() == verifier.GetType()))
        {
            throw new ArgumentException($"A plugin verifier of type '{verifier.GetType()}' has already been added.", nameof(verifier));
        }

        _verifiers.Add(verifier);
        return this;
    }

    /// <summary>
    /// Adds a verifier that has a public parameterless constructor.
    /// </summary>
    /// <typeparam name="TVerifier">The verifier type.</typeparam>
    /// <returns>This collection, for chaining.</returns>
    /// <exception cref="InvalidOperationException"><c>builder.ApplyPlugins()</c> has already been called.</exception>
    /// <exception cref="ArgumentException">A verifier of the same type has already been added.</exception>
    public PluginVerifierCollection Add<TVerifier>()
        where TVerifier : class, IPluginVerifier, new()
    {
        return Add(new TVerifier());
    }

    /// <summary>
    /// Adds the <see cref="HashVerifier"/>, which verifies plugin folders against a lock file.
    /// </summary>
    /// <param name="configure">Configures the settings in code. Without it, the lock file is <c>plugins.lock.json</c> in the content root.</param>
    /// <returns>This collection, for chaining.</returns>
    /// <example>
    /// <code language="csharp">
    /// builder.PluginVerifiers.AddHashVerifier(options => options.LockFile = "/etc/myapp/plugins.lock.json");
    /// </code>
    /// </example>
    public PluginVerifierCollection AddHashVerifier(Action<HashVerifierOptions>? configure = null)
    {
        var options = new HashVerifierOptions();
        configure?.Invoke(options);
        return AddHashVerifier(options);
    }

    /// <summary>
    /// Adds the <see cref="HashVerifier"/> with settings bound from a configuration section.
    /// </summary>
    /// <remarks>
    /// The settings are the basis of trust. If they come from <c>appsettings.json</c>, protect that file
    /// from whoever can write to the plugin directory.
    /// </remarks>
    /// <param name="section">The section with the settings, for example <c>Plugwright:Verification:Hash</c>.</param>
    /// <returns>This collection, for chaining.</returns>
    /// <example>
    /// <code language="csharp">
    /// builder.PluginVerifiers.AddHashVerifier(builder.Configuration.GetSection("Plugwright:Verification:Hash"));
    /// </code>
    /// </example>
    public PluginVerifierCollection AddHashVerifier(IConfigurationSection section)
    {
        ArgumentNullException.ThrowIfNull(section);
        return AddHashVerifier(section.Get<HashVerifierOptions>() ?? new HashVerifierOptions());
    }

    /// <summary>
    /// Adds the <see cref="CertificateVerifier"/>, which verifies the signed manifest of every plugin folder.
    /// </summary>
    /// <param name="configure">Configures which certificates are trusted. At least one thumbprint or publisher is required.</param>
    /// <returns>This collection, for chaining.</returns>
    /// <exception cref="ArgumentException">The settings trust no certificate, or a setting is invalid.</exception>
    /// <exception cref="InvalidOperationException">The issuer certificate of a trusted publisher cannot be loaded.</exception>
    /// <example>
    /// Trust one certificate by its thumbprint:
    /// <code language="csharp">
    /// builder.PluginVerifiers.AddCertificateVerifier(options =&gt;
    ///     options.TrustedThumbprints.Add("3F2504E04F8911D39A0C0305E82C3301EB2E0C3D84EF2A5E3C5B8E2A1D0F9C7B"));
    /// </code>
    /// Trust every certificate the company CA issues to the plugin team, and check for revocation:
    /// <code language="csharp">
    /// builder.PluginVerifiers.AddCertificateVerifier(options =&gt;
    /// {
    ///     options.TrustedPublishers.Add(new TrustedPublisher
    ///     {
    ///         IssuerCertificate = "/etc/myapp/company-ca.crt",
    ///         Subject = "CN=Acme Plugins, O=Acme",
    ///     });
    ///     options.RevocationMode = X509RevocationMode.Online;
    /// });
    /// </code>
    /// </example>
    public PluginVerifierCollection AddCertificateVerifier(Action<CertificateVerifierOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        var options = new CertificateVerifierOptions();
        configure(options);
        return AddCertificateVerifier(options);
    }

    /// <summary>
    /// Adds the <see cref="CertificateVerifier"/> with settings bound from a configuration section.
    /// </summary>
    /// <remarks>
    /// The settings are the basis of trust. If they come from <c>appsettings.json</c>, protect that file
    /// from whoever can write to the plugin directory.
    /// </remarks>
    /// <param name="section">The section with the settings, for example <c>Plugwright:Verification:Certificate</c>.</param>
    /// <returns>This collection, for chaining.</returns>
    /// <exception cref="ArgumentException">The settings trust no certificate, or a setting is invalid.</exception>
    /// <exception cref="InvalidOperationException">The issuer certificate of a trusted publisher cannot be loaded.</exception>
    /// <example>
    /// <code language="csharp">
    /// builder.PluginVerifiers.AddCertificateVerifier(builder.Configuration.GetSection("Plugwright:Verification:Certificate"));
    /// </code>
    /// <code language="json">
    /// {
    ///   "Plugwright": {
    ///     "Verification": {
    ///       "Certificate": {
    ///         "TrustedPublishers": [ { "IssuerCertificate": "/etc/myapp/company-ca.crt", "Subject": "CN=Acme Plugins, O=Acme" } ],
    ///         "RevocationMode": "Online"
    ///       }
    ///     }
    ///   }
    /// }
    /// </code>
    /// </example>
    public PluginVerifierCollection AddCertificateVerifier(IConfigurationSection section)
    {
        ArgumentNullException.ThrowIfNull(section);
        var options = new CertificateVerifierOptions();
        section.Bind(options);
        return AddCertificateVerifier(options);
    }

    /// <summary>
    /// Returns the registered verifiers in the order they were added.
    /// </summary>
    /// <returns>An enumerator over the verifiers.</returns>
    public IEnumerator<IPluginVerifier> GetEnumerator() => _verifiers.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    private PluginVerifierCollection AddCertificateVerifier(CertificateVerifierOptions options) =>
        Add(new CertificateVerifier(CertificateTrust.Create(options, _contentRoot)));

    private PluginVerifierCollection AddHashVerifier(HashVerifierOptions options)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(options.LockFile, nameof(options.LockFile));
        return Add(new HashVerifier(Path.GetFullPath(options.LockFile, _contentRoot)));
    }
}
