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
    }
}
