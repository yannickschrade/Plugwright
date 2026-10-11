using System.Text.Json.Nodes;
using Plugwright.AspNetCore;

namespace Plugwright.Tests;

public sealed class PluginStaticAssetEndpointsTests
{
    private const string Prefix = "_plugwright/Acme.Orders/";

    [Fact]
    public void Published_folder_puts_own_assets_below_content_and_keeps_library_assets_and_the_bundle()
    {
        var assets = Manifest("Publish", ("app.js", "app.js", "app.js"), ("_content/Lib/lib.js", "_content/Lib/lib.js", null), ("Acme.Orders.abc.styles.css", "Acme.Orders.styles.css", "Acme.Orders.styles.css"));

        var endpoints = PluginStaticAssetEndpoints.Read(assets, Prefix).ShouldNotBeNull();

        endpoints.Select(endpoint => (string)endpoint["Route"]!).ShouldBe(["_content/Acme.Orders/app.js", "_content/Lib/lib.js", "Acme.Orders.abc.styles.css"]);
        endpoints.Select(endpoint => (string)endpoint["AssetFile"]!).ShouldBe([Prefix + "app.js", Prefix + "_content/Lib/lib.js", Prefix + "Acme.Orders.styles.css"]);
        Label(endpoints[0]).ShouldBe("_content/Acme.Orders/app.js");
    }

    [Fact]
    public void Build_output_has_no_endpoints()
    {
        PluginStaticAssetEndpoints.Read(Manifest("Build", ("app.js", "app.js", null)), Prefix).ShouldBeNull();
    }

    [Fact]
    public void Package_endpoints_are_read_from_MSBuild_items()
    {
        var assets = new PluginStaticWebAssets
        {
            Id = "Acme.Orders",
            Format = PluginStaticWebAssetsFormat.PackageEndpoints,
            Endpoints = """
                <Project>
                  <ItemGroup>
                    <StaticWebAssetEndpoint Include="_content/Acme.Orders/css/site.css">
                      <AssetFile>$([System.IO.Path]::GetFullPath('$(MSBuildThisFileDirectory)..\staticwebassets\css\site.css'))</AssetFile>
                      <Selectors><![CDATA[[]]]></Selectors>
                      <EndpointProperties><![CDATA[[{"Name":"label","Value":"_content/Acme.Orders/css/site.css"}]]]></EndpointProperties>
                      <ResponseHeaders><![CDATA[[{"Name":"ETag","Value":"abc"}]]]></ResponseHeaders>
                    </StaticWebAssetEndpoint>
                  </ItemGroup>
                </Project>
                """,
        };

        var endpoint = PluginStaticAssetEndpoints.Read(assets, Prefix).ShouldNotBeNull().ShouldHaveSingleItem();

        ((string)endpoint["Route"]!).ShouldBe("_content/Acme.Orders/css/site.css");
        ((string)endpoint["AssetFile"]!).ShouldBe(Prefix + "css/site.css");
        Label(endpoint).ShouldBe("_content/Acme.Orders/css/site.css");
    }

    [Fact]
    public void Style_sheet_prefers_the_fingerprinted_route()
    {
        var assets = Manifest("Publish", ("Acme.Orders.styles.css", "Acme.Orders.styles.css", "Acme.Orders.styles.css"), ("Acme.Orders.abc.styles.css", "Acme.Orders.styles.css", "Acme.Orders.styles.css"));
        var endpoints = PluginStaticAssetEndpoints.Read(assets, Prefix)!;
        ((JsonArray)endpoints[1]["EndpointProperties"]!).Add(new JsonObject { ["Name"] = "fingerprint", ["Value"] = "abc" });

        PluginStaticAssetEndpoints.FindStyleSheet(assets, endpoints).ShouldBe("Acme.Orders.abc.styles.css");
    }

    [Fact]
    public void Compression_adds_variants_only_when_they_are_smaller()
    {
        var assets = Manifest("Publish", ("big.css", "big.css", null), ("tiny.css", "tiny.css", null));
        var endpoints = PluginStaticAssetEndpoints.Read(assets, Prefix)!;
        var files = new Dictionary<string, byte[]>
        {
            ["big.css"] = System.Text.Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat(".a { color: red; }\n", 50))),
            ["tiny.css"] = [(byte)'x'],
        };

        var compressed = PluginStaticAssetEndpoints.Compress(endpoints, files, Prefix);

        compressed.Keys.Order().ShouldBe(["big.css.br", "big.css.gz"]);
        var brotli = endpoints.Single(endpoint => (string)endpoint["AssetFile"]! == Prefix + "big.css.br");
        ((string)brotli["Route"]!).ShouldBe("_content/Acme.Orders/big.css");
        brotli["Selectors"]![0]!["Value"]!.GetValue<string>().ShouldBe("br");
        Header(brotli, "Content-Encoding").ShouldBe("br");
        Header(brotli, "Content-Length").ShouldBe(compressed["big.css.br"].Length.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    private static PluginStaticWebAssets Manifest(string type, params (string Route, string AssetFile, string? Label)[] endpoints)
    {
        var array = new JsonArray();
        foreach ((var route, var assetFile, var label) in endpoints)
        {
            var properties = new JsonArray();
            if (label is not null)
            {
                properties.Add(new JsonObject { ["Name"] = "label", ["Value"] = label });
            }

            array.Add(new JsonObject
            {
                ["Route"] = route,
                ["AssetFile"] = assetFile,
                ["Selectors"] = new JsonArray(),
                ["ResponseHeaders"] = new JsonArray(new JsonObject { ["Name"] = "Content-Length", ["Value"] = "1" }),
                ["EndpointProperties"] = properties,
            });
        }

        return new PluginStaticWebAssets
        {
            Id = "Acme.Orders",
            Format = PluginStaticWebAssetsFormat.EndpointsManifest,
            Endpoints = new JsonObject { ["Version"] = 1, ["ManifestType"] = type, ["Endpoints"] = array }.ToJsonString(),
        };
    }

    private static string? Label(JsonObject endpoint) =>
        ((JsonArray)endpoint["EndpointProperties"]!).FirstOrDefault(property => (string?)property!["Name"] == "label")?["Value"]?.GetValue<string>();

    private static string? Header(JsonObject endpoint, string name) =>
        ((JsonArray)endpoint["ResponseHeaders"]!).FirstOrDefault(header => (string?)header!["Name"] == name)?["Value"]?.GetValue<string>();
}
