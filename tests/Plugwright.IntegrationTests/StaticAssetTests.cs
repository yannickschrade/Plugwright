using System.Net;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace Plugwright.IntegrationTests;

/// <summary>
/// Static web assets of a Blazor plugin from a published folder and from a package. The plugin
/// (Plugwright.TestPackages.BlazorAssets) has a file in wwwroot, scoped CSS and a JavaScript module, and uses a component
/// library (Plugwright.TestPackages.AssetsLibrary) with assets and scoped CSS of its own.
/// </summary>
[Collection(LoadContextTests.Name)]
public sealed partial class StaticAssetTests(TestPluginsFixture testPlugins) : IDisposable
{
    private const string PluginId = "Plugwright.TestPackages.BlazorAssets";
    private const string LibraryId = "Plugwright.TestPackages.AssetsLibrary";

    private static readonly string s_publishedPluginsPath = typeof(StaticAssetTests).Assembly
        .GetCustomAttributes<AssemblyMetadataAttribute>()
        .Single(attribute => attribute.Key == "PublishedPluginsPath")
        .Value!;

    private readonly TempDirectory _directory = new();

    private string PluginDirectory => Path.Combine(_directory.Path, "plugins");

    private string PackageDirectory => Path.Combine(_directory.Path, "packages");

    private string PluginFolder => Path.Combine(PluginDirectory, PluginId);

    public void Dispose() => _directory.Dispose();

    [Theory]
    [InlineData("Disk")]
    [InlineData("Memory")]
    public async Task Assets_of_a_published_plugin_folder_are_served_like_those_of_a_project_reference(string source)
    {
        InstallPublishedPlugin();
        using var host = StartHost(Directory(source));
        var client = host.CreateClient();

        (await GetStringAsync(client, $"/_content/{PluginId}/plugin.css")).ShouldContain("rebeccapurple");
        (await GetStringAsync(client, $"/_content/{PluginId}/Pages/Assets.razor.js")).ShouldContain("page");
        (await GetStringAsync(client, $"/_content/{LibraryId}/library.js")).ShouldContain("library");

        using var compressed = await GetAsync(client, $"/_content/{PluginId}/plugin.css", acceptEncoding: "br");
        compressed.Content.Headers.ContentEncoding.ShouldBe(["br"]);
        compressed.Headers.ETag.ShouldNotBeNull();
    }

    [Fact]
    public async Task Scoped_css_of_a_published_plugin_is_linked_and_imports_its_library()
    {
        InstallPublishedPlugin();
        using var host = StartHost(Directory("Disk"));
        var client = host.CreateClient();

        var styleSheets = StyleSheets(await GetStringAsync(client, "/"));

        var styleSheet = styleSheets.ShouldHaveSingleItem();
        styleSheet.ShouldStartWith($"{PluginId}.");
        styleSheet.ShouldEndWith(".styles.css");
        using var response = await GetAsync(client, "/" + styleSheet);
        response.Headers.CacheControl!.ToString().ShouldContain("immutable");
        var css = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        css.ShouldContain("color: navy");

        // The bundle imports the library's bundle with a path relative to itself, at the root.
        var import = ImportPattern().Match(css).Groups["path"].Value;
        import.ShouldStartWith($"_content/{LibraryId}/");
        (await GetStringAsync(client, "/" + import)).ShouldContain("color: teal");
    }

    [Fact]
    public async Task Plugin_page_from_a_folder_renders()
    {
        InstallPublishedPlugin();
        using var host = StartHost(Directory("Disk"));

        var page = await GetStringAsync(host.CreateClient(), "/blazor-assets");

        page.ShouldContain("Plugin with assets");
        page.ShouldContain("from the library");
    }

    [Theory]
    [InlineData("Directory")]
    [InlineData("Package")]
    public async Task Assets_and_ImportMap_know_the_plugin_assets_and_keep_the_host_assets(string kind)
    {
        Dictionary<string, string?> settings;
        if (kind == "Directory")
        {
            InstallPublishedPlugin();
            settings = Directory("Disk");
        }
        else
        {
            InstallPackages();
            settings = Packages("Disk", compress: false);
        }

        using var host = StartHost(settings);
        var client = host.CreateClient();
        var page = await GetStringAsync(client, "/blazor-assets");

        // @Assets in the plugin's page returns the fingerprinted route, which is served for a year.
        var pluginStyleSheet = Regex.Match(page, "id=\"plugin-css\" rel=\"preload\" href=\"(?<href>[^\"]+)\"").Groups["href"].Value;
        pluginStyleSheet.ShouldMatch($"^_content/{Regex.Escape(PluginId)}/plugin\\.[a-z0-9]+\\.css$");
        using var response = await GetAsync(client, "/" + pluginStyleSheet);
        response.Headers.CacheControl!.ToString().ShouldContain("immutable");

        // The host's own assets keep their fingerprints, and the import map knows the plugin's module.
        Regex.Match(page, "id=\"host-script\" src=\"(?<src>[^\"]+)\"").Groups["src"].Value.ShouldMatch("^_framework/blazor\\.web\\.[a-z0-9]+\\.js$");
        page.ShouldContain($"_content/{PluginId}/Pages/Assets.razor.js");
    }

    [Fact]
    public async Task Memory_serves_the_verified_bytes_and_Disk_serves_the_file()
    {
        InstallPublishedPlugin();
        var styleSheetPath = Path.Combine(PluginFolder, "wwwroot", "plugin.css");
        var original = File.ReadAllText(styleSheetPath);
        // Same length: the endpoints take Content-Length from the manifest, so a file of another length fails to send.
        var tampered = original.Replace("color: rebeccapurple;", "color: red;          ", StringComparison.Ordinal);
        LockFileWriter.Write(Path.Combine(_directory.Path, "plugins.lock.json"), PluginDirectory);
        var settings = Directory("Memory");
        settings["Plugwright:Verification:Hash:LockFile"] = Path.Combine(_directory.Path, "plugins.lock.json");

        using (var host = StartHost(settings))
        {
            var client = host.CreateClient();
            File.WriteAllText(styleSheetPath, tampered);

            (await GetStringAsync(client, $"/_content/{PluginId}/plugin.css")).ShouldContain("rebeccapurple");
        }

        File.WriteAllText(styleSheetPath, original);
        settings["TestWebHost:StaticAssets:Source"] = "Disk";
        using (var host = StartHost(settings))
        {
            var client = host.CreateClient();
            File.WriteAllText(styleSheetPath, tampered);

            (await GetStringAsync(client, $"/_content/{PluginId}/plugin.css")).ShouldContain("color: red;");
        }
    }

    [Theory]
    [InlineData("Disk")]
    [InlineData("Memory")]
    public async Task Assets_of_a_plugin_package_and_its_library_package_are_served(string source)
    {
        InstallPackages();
        using var host = StartHost(Packages(source, compress: false));
        var client = host.CreateClient();

        (await GetStringAsync(client, $"/_content/{PluginId}/plugin.css")).ShouldContain("rebeccapurple");
        (await GetStringAsync(client, $"/_content/{LibraryId}/library.js")).ShouldContain("library");
        using var uncompressed = await GetAsync(client, $"/_content/{PluginId}/plugin.css", acceptEncoding: "br");
        uncompressed.Content.Headers.ContentEncoding.ShouldBeEmpty();

        // Packages bring a scoped CSS bundle each, the plugin's and the library's.
        var styleSheets = StyleSheets(await GetStringAsync(client, "/"));
        styleSheets.Count.ShouldBe(2);
        styleSheets.ShouldAllBe(styleSheet => styleSheet.EndsWith(".bundle.scp.css"));
        (await GetStringAsync(client, "/" + styleSheets.Single(path => path.Contains(PluginId)))).ShouldContain("color: navy");
        (await GetStringAsync(client, "/" + styleSheets.Single(path => path.Contains(LibraryId)))).ShouldContain("color: teal");
    }

    [Theory]
    [InlineData("Disk")]
    [InlineData("Memory")]
    public async Task Assets_of_packages_can_be_compressed_at_startup(string source)
    {
        InstallPackages();
        using var host = StartHost(Packages(source, compress: true));
        var client = host.CreateClient();

        using var brotli = await GetAsync(client, $"/_content/{PluginId}/plugin.css", acceptEncoding: "br");
        brotli.Content.Headers.ContentEncoding.ShouldBe(["br"]);
        using var gzip = await GetAsync(client, $"/_content/{LibraryId}/library.js", acceptEncoding: "gzip");
        gzip.Content.Headers.ContentEncoding.ShouldBe(["gzip"]);
        using var plain = await GetAsync(client, $"/_content/{PluginId}/plugin.css");
        plain.Content.Headers.ContentEncoding.ShouldBeEmpty();
        (await plain.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).ShouldContain("rebeccapurple");
    }

    [Fact]
    public async Task Build_output_is_skipped_with_a_warning()
    {
        InstallPublishedPlugin();
        // The build output has a manifest of type Build and no asset files.
        var manifestPath = Path.Combine(PluginFolder, $"{PluginId}.staticwebassets.endpoints.json");
        File.WriteAllText(manifestPath, File.ReadAllText(manifestPath).Replace("\"Publish\"", "\"Build\"", StringComparison.Ordinal));
        System.IO.Directory.Delete(Path.Combine(PluginFolder, "wwwroot"), recursive: true);

        using var host = StartHost(Directory("Disk"));

        using var response = await GetAsync(host.CreateClient(), $"/_content/{PluginId}/plugin.css");
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        host.Services.GetFakeLogCollector().GetSnapshot()
            .ShouldContain(record => record.Id.Id == 1014 && record.Level == LogLevel.Warning && record.Message.Contains(PluginId));
    }

    private void InstallPublishedPlugin()
    {
        var source = Path.Combine(s_publishedPluginsPath, PluginId);
        foreach (var file in System.IO.Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            var target = Path.Combine(PluginFolder, Path.GetRelativePath(source, file));
            System.IO.Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target);
        }
    }

    private void InstallPackages()
    {
        testPlugins.InstallPackage($"{PluginId}.1.0.0", PackageDirectory);
        testPlugins.InstallPackage($"{LibraryId}.1.0.0", PackageDirectory);
    }

    private Dictionary<string, string?> Directory(string source) => new()
    {
        ["Plugwright:Directories:0"] = PluginDirectory,
        ["TestWebHost:StaticAssets:Source"] = source,
    };

    private Dictionary<string, string?> Packages(string source, bool compress) => new()
    {
        ["Plugwright:Packages:0:Path"] = PackageDirectory,
        ["TestWebHost:StaticAssets:Source"] = source,
        ["TestWebHost:StaticAssets:Compress"] = compress ? "true" : "false",
    };

    private static WebTestHost StartHost(Dictionary<string, string?> settings)
    {
        settings["Plugwright:SettingsFile"] = "";
        return new WebTestHost(settings);
    }

    private static async Task<HttpResponseMessage> GetAsync(HttpClient client, string path, string? acceptEncoding = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        if (acceptEncoding is not null)
        {
            request.Headers.AcceptEncoding.Add(new StringWithQualityHeaderValue(acceptEncoding));
        }

        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
        return response;
    }

    private static async Task<string> GetStringAsync(HttpClient client, string path)
    {
        using var response = await GetAsync(client, path);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, path);
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        content.ShouldNotBeEmpty(path);
        return content;
    }

    private static List<string> StyleSheets(string page) =>
        [.. StyleSheetPattern().Matches(page).Select(match => match.Groups["href"].Value)];

    [GeneratedRegex("<link rel=\"stylesheet\" href=\"(?<href>[^\"]+)\"")]
    private static partial Regex StyleSheetPattern();

    [GeneratedRegex("@import '(?<path>[^']+)'")]
    private static partial Regex ImportPattern();
}
