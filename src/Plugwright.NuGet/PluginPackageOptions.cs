namespace Plugwright.NuGet;

/// <summary>
/// Options of a package folder added with <see cref="PluginPackageExtensions.AddFromPackages(PluginCollection, string, Action{PluginPackageOptions})"/>.
/// </summary>
public sealed class PluginPackageOptions
{
    /// <summary>
    /// Gets or sets a NuGet feed to download missing library packages from: a URL such as
    /// <c>https://api.nuget.org/v3/index.json</c>, a local folder, or the name of a package source in NuGet.Config.
    /// The default is <see langword="null"/>: nothing is downloaded, and all dependencies must be in the package folder
    /// or provided by the host.
    /// </summary>
    /// <remarks>
    /// Only library packages are downloaded, in the lowest version that matches. Plugin packages are never downloaded;
    /// install them in the package folder. Credentials for a feed come from NuGet.Config.
    /// </remarks>
    public string? Feed { get; set; }

    /// <summary>
    /// Gets or sets the folder that downloaded packages are stored in, so that the next start needs no network.
    /// A relative path is resolved against the host's content root. The default is <c>plugin-package-downloads</c>.
    /// </summary>
    public string DownloadDirectory { get; set; } = "plugin-package-downloads";

    /// <summary>
    /// Gets or sets how long downloading one package may take before it fails. The default is 30 seconds.
    /// </summary>
    public TimeSpan DownloadTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets or sets whether packages from the feed must pass the registered verifiers, like the packages in the
    /// package folder. This applies to new downloads and to packages already in the download folder.
    /// The default is <see langword="true"/>.
    /// </summary>
    /// <remarks>
    /// Turn it off only if you trust the feed itself, for example a feed of your own company: packages from the feed then
    /// run without verification. Packages in the package folder are always verified when verifiers are registered.
    /// </remarks>
    public bool VerifyDownloads { get; set; } = true;
}
