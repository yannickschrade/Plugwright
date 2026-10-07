using Microsoft.Extensions.Configuration;

namespace Plugwright;

/// <summary>
/// Adds NuGet package folders as a plugin source to <c>builder.Plugins</c>.
/// </summary>
public static class PluginPackageExtensions
{
    extension(PluginCollection plugins)
    {
        /// <summary>
        /// Adds all plugin packages from a folder of NuGet packages (.nupkg files).
        /// </summary>
        /// <remarks>
        /// <para>
        /// A package is a plugin if it has the package type <c>PlugwrightPlugin</c>. It must contain an assembly named like
        /// the package ID for net10.0. All other packages in the folder are library packages: they are only loaded when a
        /// plugin package depends on them, in the lowest version that matches. Dependencies that the host already provides,
        /// such as Microsoft.Extensions.*, need no package. With <see cref="PluginPackageOptions.Feed"/>, missing library
        /// packages are downloaded.
        /// </para>
        /// <para>
        /// Packages are read into memory and loaded from there; nothing is extracted, except native libraries, which go to
        /// a temporary folder of the process. Each plugin package gets its own load context. A relative path is resolved
        /// against the host's content root. If the folder does not exist, a warning is logged.
        /// </para>
        /// </remarks>
        /// <param name="path">The package folder, absolute or relative to the content root.</param>
        /// <param name="configure">Configures the package folder, for example a feed for missing library packages.</param>
        /// <returns>The collection, for chaining.</returns>
        /// <exception cref="InvalidOperationException"><c>builder.ApplyPlugins()</c> has already been called.</exception>
        /// <exception cref="ArgumentException">The path is empty.</exception>
        /// <example>
        /// <code language="csharp">
        /// builder.Plugins.AddFromPackages("packages");
        ///
        /// builder.Plugins.AddFromPackages("packages", options =&gt; options.Feed = "https://api.nuget.org/v3/index.json");
        /// </code>
        /// </example>
        public PluginCollection AddFromPackages(string path, Action<PluginPackageOptions>? configure = null)
        {
            ArgumentNullException.ThrowIfNull(plugins);
            ArgumentException.ThrowIfNullOrWhiteSpace(path);

            var options = new PluginPackageOptions();
            configure?.Invoke(options);
            ArgumentException.ThrowIfNullOrWhiteSpace(options.DownloadDirectory, nameof(options.DownloadDirectory));

            return plugins.AddSource(new PluginPackageSource(path, options));
        }

        /// <summary>
        /// Adds a package folder with its options from configuration.
        /// </summary>
        /// <remarks>
        /// The section is either the path of the package folder, or an object with <c>Path</c> and the properties of
        /// <see cref="PluginPackageOptions"/>. Any other key is an error.
        /// </remarks>
        /// <param name="section">The section of one package folder.</param>
        /// <returns>The collection, for chaining.</returns>
        /// <exception cref="InvalidOperationException">The section has no path, an unknown key or an invalid value.</exception>
        /// <example>
        /// <code language="json">
        /// {
        ///   "Path": "packages",
        ///   "Feed": "https://api.nuget.org/v3/index.json",
        ///   "DownloadTimeout": "00:01:00",
        ///   "VerifyDownloads": true
        /// }
        /// </code>
        /// </example>
        public PluginCollection AddFromPackages(IConfigurationSection section)
        {
            ArgumentNullException.ThrowIfNull(plugins);
            ArgumentNullException.ThrowIfNull(section);

            if (section.Value is { } path)
            {
                return plugins.AddFromPackages(path);
            }

            string[] knownKeys = ["Path", nameof(PluginPackageOptions.Feed), nameof(PluginPackageOptions.DownloadDirectory),
                nameof(PluginPackageOptions.DownloadTimeout), nameof(PluginPackageOptions.VerifyDownloads)];
            if (section.GetChildren().FirstOrDefault(child => !knownKeys.Contains(child.Key, StringComparer.OrdinalIgnoreCase)) is { } unknown)
            {
                throw new InvalidOperationException(
                    $"The configuration '{unknown.Path}' is not known. Known keys are: {string.Join(", ", knownKeys)}.");
            }

            var packagePath = section["Path"]
                ?? throw new InvalidOperationException($"The configuration '{section.Path}' needs the path of the package folder in 'Path'.");
            return plugins.AddFromPackages(packagePath, options => section.Bind(options));
        }

        /// <summary>
        /// Lets <c>AddFromConfiguration</c> read package folders (<c>Packages</c>) and the package signature verifier
        /// (<c>Verification:PackageSignature</c>). Call it before reading the configuration.
        /// </summary>
        /// <returns>The collection, for chaining.</returns>
        /// <example>
        /// <code language="csharp">
        /// builder.Plugins.AddNuGetSupport();
        ///
        /// var section = builder.Configuration.GetSection("Plugwright");
        /// builder.Plugins.AddFromConfiguration(section);
        /// builder.PluginVerifiers.AddFromConfiguration(section.GetSection("Verification"));
        /// </code>
        /// <code language="json">
        /// {
        ///   "Plugwright": {
        ///     "Packages": [ { "Path": "packages", "Feed": "https://api.nuget.org/v3/index.json" } ],
        ///     "Verification": {
        ///       "PackageSignature": { "TrustedThumbprints": [ "9F2DB14A363EBF6A1DA40F4229766BD35E55261904EBD500AEE48B0C8869EB60" ] }
        ///     }
        ///   }
        /// }
        /// </code>
        /// </example>
        public PluginCollection AddNuGetSupport()
        {
            ArgumentNullException.ThrowIfNull(plugins);

            plugins.ConfigurationReaders.Sources["Packages"] = (collection, section) =>
            {
                foreach (var package in section.GetChildren())
                {
                    collection.AddFromPackages(package);
                }
            };
            plugins.ConfigurationReaders.Verifiers["PackageSignature"] = (verifiers, section) => verifiers.AddPackageSignatureVerifier(section);
            return plugins;
        }
    }
}
