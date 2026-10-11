using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using Microsoft.AspNetCore.Components;

namespace Plugwright.AspNetCore;

/// <summary>
/// Turns the endpoint descriptions of plugin folders and packages into entries of a static assets endpoints manifest,
/// the format that <c>MapStaticAssets</c> reads.
/// </summary>
/// <remarks>
/// Routes end up where a project reference would put them: the plugin's own assets below <c>_content/{Id}/</c>, assets
/// of referenced libraries below their own <c>_content/</c> path. The scoped CSS bundle of a published plugin stays at
/// the root, because it imports the bundles of its libraries with relative paths. Asset files are looked up below
/// <c>assetPrefix</c> in the web root file provider.
/// </remarks>
internal static class PluginStaticAssetEndpoints
{
    private const string ContentPrefix = "_content/";

    /// <summary>
    /// Reads the endpoints of one set of assets, or returns <see langword="null"/> for the build output of a plugin
    /// folder, which does not contain the asset files.
    /// </summary>
    public static List<JsonObject>? Read(PluginStaticWebAssets assets, string assetPrefix) => assets.Format switch
    {
        PluginStaticWebAssetsFormat.EndpointsManifest => ReadManifest(assets, assetPrefix),
        _ => ReadPackage(assets, assetPrefix),
    };

    /// <summary>
    /// Returns the route of the scoped CSS bundle of a set of assets, preferring the fingerprinted one, or
    /// <see langword="null"/> if it has none.
    /// </summary>
    public static string? FindStyleSheet(PluginStaticWebAssets assets, IEnumerable<JsonObject> endpoints)
    {
        var bundle = assets.Format == PluginStaticWebAssetsFormat.EndpointsManifest
            ? $"{assets.Id}.styles.css"
            : ".bundle.scp.css";
        return endpoints
            .Where(endpoint => endpoint["Selectors"] is not JsonArray { Count: > 0 })
            .Where(endpoint => (Property(endpoint, "label") ?? (string)endpoint["Route"]!).EndsWith(bundle, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(endpoint => Property(endpoint, "fingerprint") is not null)
            .Select(endpoint => (string)endpoint["Route"]!)
            .FirstOrDefault();
    }

    /// <summary>
    /// Returns the endpoints as Blazor resources, as Blazor builds them from a manifest: one per endpoint without selectors,
    /// with its label and integrity.
    /// </summary>
    public static IEnumerable<ResourceAsset> ToResources(IEnumerable<JsonObject> endpoints) =>
        endpoints
            .Where(endpoint => endpoint["Selectors"] is not JsonArray { Count: > 0 })
            .Select(endpoint => new ResourceAsset(
                (string)endpoint["Route"]!,
                [.. ((JsonArray?)endpoint["EndpointProperties"] ?? [])
                    .Where(property => (string?)property!["Name"] is "label" or "integrity")
                    .Select(property => new ResourceAssetProperty((string)property!["Name"]!, (string)property["Value"]!))]));

    /// <summary>
    /// Adds Brotli and gzip variants of every asset that becomes smaller, like <c>dotnet publish</c> does.
    /// </summary>
    /// <returns>The compressed files, by path relative to the asset root.</returns>
    public static Dictionary<string, byte[]> Compress(List<JsonObject> endpoints, IReadOnlyDictionary<string, byte[]> files, string assetPrefix)
    {
        var compressed = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var endpoint in endpoints.Where(endpoint => endpoint["Selectors"] is not JsonArray { Count: > 0 }).ToList())
        {
            var assetFile = ((string)endpoint["AssetFile"]!)[assetPrefix.Length..];
            if (!files.TryGetValue(assetFile, out var content))
            {
                continue;
            }

            foreach ((var encoding, var extension) in new[] { ("br", ".br"), ("gzip", ".gz") })
            {
                var compressedPath = assetFile + extension;
                if (!compressed.TryGetValue(compressedPath, out var compressedContent))
                {
                    compressedContent = CompressContent(content, encoding);
                    if (compressedContent.Length >= content.Length)
                    {
                        continue;
                    }

                    compressed[compressedPath] = compressedContent;
                }

                endpoints.Add(CompressedVariant(endpoint, assetPrefix + compressedPath, encoding, compressedContent));
            }
        }

        return compressed;
    }

    private static List<JsonObject>? ReadManifest(PluginStaticWebAssets assets, string assetPrefix)
    {
        var manifest = JsonNode.Parse(assets.Endpoints)!.AsObject();
        if (string.Equals((string?)manifest["ManifestType"], "Build", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        List<JsonObject> endpoints = [];
        foreach (var node in manifest["Endpoints"]!.AsArray())
        {
            var endpoint = node!.DeepClone().AsObject();
            var route = (string)endpoint["Route"]!;
            var label = Property(endpoint, "label") ?? route;
            var isStyleSheetBundle = label.Equals($"{assets.Id}.styles.css", StringComparison.OrdinalIgnoreCase);
            if (!route.StartsWith(ContentPrefix, StringComparison.OrdinalIgnoreCase) && !isStyleSheetBundle)
            {
                endpoint["Route"] = $"{ContentPrefix}{assets.Id}/{route}";
                SetProperty(endpoint, "label", value => $"{ContentPrefix}{assets.Id}/{value}");
            }

            endpoint["AssetFile"] = assetPrefix + (string)endpoint["AssetFile"]!;
            endpoints.Add(endpoint);
        }

        return endpoints;
    }

    private static List<JsonObject> ReadPackage(PluginStaticWebAssets assets, string assetPrefix)
    {
        List<JsonObject> endpoints = [];
        foreach (var item in XDocument.Parse(assets.Endpoints).Descendants().Where(element => element.Name.LocalName == "StaticWebAssetEndpoint"))
        {
            endpoints.Add(new JsonObject
            {
                ["Route"] = (string)item.Attribute("Include")!,
                ["AssetFile"] = assetPrefix + PackageAssetFile(Element(item, "AssetFile")),
                ["Selectors"] = JsonNode.Parse(Element(item, "Selectors")),
                ["ResponseHeaders"] = JsonNode.Parse(Element(item, "ResponseHeaders")),
                ["EndpointProperties"] = JsonNode.Parse(Element(item, "EndpointProperties")),
            });
        }

        return endpoints;
    }

    // "$([System.IO.Path]::GetFullPath('$(MSBuildThisFileDirectory)..\staticwebassets\css\site.css'))" -> "css/site.css"
    private static string PackageAssetFile(string value)
    {
        const string Folder = "staticwebassets";
        var start = value.IndexOf(Folder, StringComparison.OrdinalIgnoreCase) + Folder.Length + 1;
        var end = value.IndexOf('\'', start);
        return value[start..(end < 0 ? value.Length : end)].Replace('\\', '/');
    }

    private static string Element(XElement item, string name) =>
        item.Elements().FirstOrDefault(element => element.Name.LocalName == name)?.Value ?? "[]";

    private static JsonObject CompressedVariant(JsonObject endpoint, string assetFile, string encoding, byte[] content)
    {
        var variant = endpoint.DeepClone().AsObject();
        variant["AssetFile"] = assetFile;
        variant["Selectors"] = new JsonArray(new JsonObject
        {
            ["Name"] = "Content-Encoding",
            ["Value"] = encoding,
            ["Quality"] = (1.0 / (content.Length + 1)).ToString("F12", CultureInfo.InvariantCulture),
        });

        var originalETag = Header(variant, "ETag");
        SetHeader(variant, "Content-Length", content.Length.ToString(CultureInfo.InvariantCulture));
        SetHeader(variant, "Content-Encoding", encoding);
        SetHeader(variant, "ETag", $"\"{Convert.ToBase64String(SHA256.HashData(content))}\"");
        if (originalETag is not null)
        {
            ((JsonArray)variant["EndpointProperties"]!).Add(new JsonObject { ["Name"] = "original-resource", ["Value"] = originalETag });
        }

        return variant;
    }

    private static byte[] CompressContent(byte[] content, string encoding)
    {
        using var output = new MemoryStream();
        using (Stream compressor = encoding == "br"
            ? new BrotliStream(output, CompressionLevel.SmallestSize, leaveOpen: true)
            : new GZipStream(output, CompressionLevel.SmallestSize, leaveOpen: true))
        {
            compressor.Write(content);
        }

        return output.ToArray();
    }

    private static string? Property(JsonObject endpoint, string name) =>
        (endpoint["EndpointProperties"] as JsonArray)?.FirstOrDefault(property => (string?)property!["Name"] == name)?["Value"]?.GetValue<string>();

    private static void SetProperty(JsonObject endpoint, string name, Func<string, string> change)
    {
        foreach (var property in (endpoint["EndpointProperties"] as JsonArray ?? []).Where(property => (string?)property!["Name"] == name))
        {
            property!["Value"] = change((string)property["Value"]!);
        }
    }

    private static string? Header(JsonObject endpoint, string name) =>
        (endpoint["ResponseHeaders"] as JsonArray)?.FirstOrDefault(header => (string?)header!["Name"] == name)?["Value"]?.GetValue<string>();

    private static void SetHeader(JsonObject endpoint, string name, string value)
    {
        var headers = (JsonArray)endpoint["ResponseHeaders"]!;
        if (headers.FirstOrDefault(header => (string?)header!["Name"] == name) is { } header)
        {
            header["Value"] = value;
        }
        else
        {
            headers.Add(new JsonObject { ["Name"] = name, ["Value"] = value });
        }
    }
}
