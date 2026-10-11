namespace Plugwright;

/// <summary>
/// How a set of static web assets describes its endpoints.
/// </summary>
internal enum PluginStaticWebAssetsFormat
{
    /// <summary>
    /// The <c>{Name}.staticwebassets.endpoints.json</c> of a published plugin folder. Its own routes have no base path;
    /// routes of referenced libraries start with <c>_content/</c>.
    /// </summary>
    EndpointsManifest,

    /// <summary>
    /// The <c>build/Microsoft.AspNetCore.StaticWebAssetEndpoints.props</c> of a NuGet package: MSBuild items whose
    /// routes include the base path <c>_content/{PackageId}/</c>.
    /// </summary>
    PackageEndpoints,
}

/// <summary>
/// The static web assets of a plugin folder or package, as found in phase 1. Plugwright reads them, but only
/// Plugwright.AspNetCore interprets and maps them.
/// </summary>
internal sealed class PluginStaticWebAssets
{
    /// <summary>
    /// Gets the ID of the plugin, or of the package for a library package of a plugin package.
    /// </summary>
    public required string Id { get; init; }

    public required PluginStaticWebAssetsFormat Format { get; init; }

    /// <summary>
    /// Gets the endpoint description: JSON or MSBuild XML, depending on <see cref="Format"/>.
    /// </summary>
    public required string Endpoints { get; init; }

    /// <summary>
    /// Gets the folder the asset files are in on disk, or <see langword="null"/> for a package, whose files exist only in
    /// memory.
    /// </summary>
    public string? Folder { get; init; }

    /// <summary>
    /// Gets or sets the asset files by path relative to the asset root, with '/' as separator: the verified bytes of a
    /// plugin folder, or the files of a package. <see langword="null"/> for a folder without verification. Set to
    /// <see langword="null"/> once the files were written elsewhere, so that they can be collected.
    /// </summary>
    public IReadOnlyDictionary<string, byte[]>? Files { get; set; }
}
