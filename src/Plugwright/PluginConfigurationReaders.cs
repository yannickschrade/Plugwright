using Microsoft.Extensions.Configuration;

namespace Plugwright;

/// <summary>
/// What <c>AddFromConfiguration</c> understands: sources next to <c>Directories</c> and verifiers in <c>Verification</c>.
/// Packages that bring sources or verifiers, such as Plugwright.NuGet, register their readers here.
/// </summary>
internal sealed class PluginConfigurationReaders
{
    /// <summary>
    /// Keys that only a package understands, with the package and the call that registers it, for a helpful error.
    /// </summary>
    private static readonly Dictionary<string, string> s_keysOfOtherPackages = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Packages"] = "Plugwright.NuGet",
        ["PackageSignature"] = "Plugwright.NuGet",
    };

    public PluginConfigurationReaders()
    {
        Verifiers["Hash"] = (verifiers, section) => verifiers.AddHashVerifier(section);
        Verifiers["Certificate"] = (verifiers, section) => verifiers.AddCertificateVerifier(section);
    }

    /// <summary>
    /// Gets the readers for source sections, such as <c>Packages</c>, by key.
    /// </summary>
    public Dictionary<string, Action<PluginCollection, IConfigurationSection>> Sources { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets the readers for verifier sections in <c>Verification</c>, such as <c>Hash</c>, by key.
    /// </summary>
    public Dictionary<string, Action<PluginVerifierCollection, IConfigurationSection>> Verifiers { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Returns the error for a key that no reader understands.
    /// </summary>
    public static InvalidOperationException UnknownKey(IConfigurationSection section, IEnumerable<string> knownKeys) =>
        s_keysOfOtherPackages.TryGetValue(section.Key, out var package)
            ? new InvalidOperationException(
                $"The configuration '{section.Path}' needs {package}. Reference it and call builder.Plugins.AddNuGetSupport() "
                + "before reading the configuration.")
            : new InvalidOperationException(
                $"The configuration '{section.Path}' is not known. Known keys are: {string.Join(", ", knownKeys.Order(StringComparer.Ordinal))}.");

    /// <summary>
    /// Returns whether a verifier section turns its verifier on: it has settings, or it is <c>true</c>.
    /// An empty JSON object creates no configuration keys, so <c>"Hash": true</c> turns a verifier on with default settings.
    /// </summary>
    /// <exception cref="InvalidOperationException">The section has a value other than <c>true</c> or <c>false</c>.</exception>
    public static bool IsEnabled(IConfigurationSection section)
    {
        if (section.GetChildren().Any())
        {
            return true;
        }

        return bool.TryParse(section.Value, out var enabled)
            ? enabled
            : throw new InvalidOperationException(
                $"The configuration '{section.Path}' must have settings, or be true or false, but is '{section.Value}'.");
    }
}
