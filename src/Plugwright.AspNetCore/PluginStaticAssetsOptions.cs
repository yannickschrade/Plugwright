namespace Plugwright.AspNetCore;

/// <summary>
/// Options for <c>app.MapPluginStaticAssets()</c>.
/// </summary>
public sealed class PluginStaticAssetsOptions
{
    /// <summary>
    /// Gets or sets where the assets are served from. The default is <see cref="PluginStaticAssetSource.Disk"/>.
    /// </summary>
    public PluginStaticAssetSource Source { get; set; } = PluginStaticAssetSource.Disk;

    /// <summary>
    /// Gets or sets whether the assets of plugin packages are compressed with Brotli and gzip at startup. The default is
    /// <see langword="false"/>.
    /// </summary>
    /// <remarks>
    /// NuGet packages contain their assets uncompressed; an application that references them compresses them when it is
    /// published. For plugin packages, Plugwright can do the same at startup, which takes time and, with
    /// <see cref="PluginStaticAssetSource.Memory"/>, memory for the compressed copies. Published plugin folders already
    /// contain compressed assets.
    /// </remarks>
    public bool CompressPackageAssets { get; set; }
}
