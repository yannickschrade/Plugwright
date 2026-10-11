using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.StaticAssets;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;

namespace Plugwright.AspNetCore;

/// <summary>
/// Maps the static web assets of the loaded plugins from directories and packages, and knows their scoped CSS bundles.
/// Registered by <see cref="BlazorPluginHandler"/>, so that <see cref="PluginStyleSheets"/> can use it.
/// </summary>
internal sealed class PluginStaticAssets
{
    private readonly Lock _lock = new();
    private IReadOnlyList<string>? _styleSheets;

    /// <summary>
    /// Gets the routes of the scoped CSS bundles of the plugins.
    /// </summary>
    /// <exception cref="InvalidOperationException"><c>app.MapPluginStaticAssets()</c> was not called.</exception>
    public IReadOnlyList<string> StyleSheets => _styleSheets
        ?? throw new InvalidOperationException(
            "The static web assets of plugins are not mapped. Call app.MapPluginStaticAssets() after app.UsePlugins().");

    /// <summary>
    /// Gets the plugins' assets as Blazor resources, for <c>@Assets</c> and <c>ImportMap</c>. Empty until
    /// <c>app.MapPluginStaticAssets()</c> was called.
    /// </summary>
    public IReadOnlyList<ResourceAsset> Resources { get; private set; } = [];

    public StaticAssetsEndpointConventionBuilder Map(IEndpointRouteBuilder endpoints, PluginStaticAssetsOptions options)
    {
        lock (_lock)
        {
            if (_styleSheets is not null)
            {
                throw new InvalidOperationException("app.MapPluginStaticAssets() has already been called. Call it once.");
            }

            var services = endpoints.ServiceProvider;
            var system = services.GetService<PluginSystem>()
                ?? throw new InvalidOperationException("app.MapPluginStaticAssets() needs builder.ApplyPlugins() before builder.Build().");
            var environment = services.GetRequiredService<IWebHostEnvironment>();
            var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger(PluginSystem.LoggerCategory);

            var hostRoutes = ReadHostRoutes(environment);
            var routes = new HashSet<string>(hostRoutes, StringComparer.OrdinalIgnoreCase);
            var manifestEndpoints = new JsonArray();
            List<IFileProvider> providers = [];
            List<string> styleSheets = [];
            List<ResourceAsset> resources = [];
            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var assets in system.StaticWebAssets.Where(assets => ids.Add(assets.Id)))
            {
                var assetPrefix = $"_plugwright/{assets.Id}/";
                var assetEndpoints = PluginStaticAssetEndpoints.Read(assets, assetPrefix);
                if (assetEndpoints is null)
                {
                    PluginStaticAssetsLog.NotPublished(logger, assets.Id);
                    continue;
                }

                var compressed = options.CompressPackageAssets && assets is { Format: PluginStaticWebAssetsFormat.PackageEndpoints, Files: { } files }
                    ? PluginStaticAssetEndpoints.Compress(assetEndpoints, files, assetPrefix)
                    : [];

                // A route the application or an earlier plugin serves, for example of a shared component library, stays theirs.
                List<JsonObject> accepted = [];
                var ownRoutesOfApplication = 0;
                foreach (var group in assetEndpoints.GroupBy(endpoint => (string)endpoint["Route"]!, StringComparer.OrdinalIgnoreCase))
                {
                    if (routes.Contains(group.Key))
                    {
                        PluginStaticAssetsLog.RouteSkipped(logger, assets.Id, group.Key);
                        if (hostRoutes.Contains(group.Key) && group.Key.StartsWith($"_content/{assets.Id}/", StringComparison.OrdinalIgnoreCase))
                        {
                            ownRoutesOfApplication++;
                        }

                        continue;
                    }

                    accepted.AddRange(group);
                }

                // The application serving a plugin's own assets means that it references the plugin, which is a mistake.
                if (ownRoutesOfApplication > 0)
                {
                    PluginStaticAssetsLog.ServedByApplication(logger, assets.Id, ownRoutesOfApplication);
                }

                routes.UnionWith(accepted.Select(endpoint => (string)endpoint["Route"]!));
                foreach (var endpoint in accepted)
                {
                    manifestEndpoints.Add(endpoint);
                }

                resources.AddRange(PluginStaticAssetEndpoints.ToResources(accepted));
                providers.Add(new PrefixFileProvider(assetPrefix, CreateFileProvider(assets, compressed, options.Source)));
                if (PluginStaticAssetEndpoints.FindStyleSheet(assets, accepted) is { } styleSheet)
                {
                    styleSheets.Add(styleSheet);
                }
            }

            // MapStaticAssets reads the asset files through the web root file provider.
            environment.WebRootFileProvider = new CompositeFileProvider([environment.WebRootFileProvider, .. providers]);

            var manifestPath = Path.Combine(PluginAssetFolder.Path, "plugins.staticwebassets.endpoints.json");
            var manifest = new JsonObject { ["Version"] = 1, ["ManifestType"] = "Publish", ["Endpoints"] = manifestEndpoints };
            File.WriteAllText(manifestPath, manifest.ToJsonString());

            Resources = resources;
            _styleSheets = styleSheets;
            return endpoints.MapStaticAssets(manifestPath);
        }
    }

    /// <summary>
    /// Creates the file provider for one set of assets: from memory, from the plugin folder, or from a temporary folder
    /// the assets of a package are written to.
    /// </summary>
    private static IFileProvider CreateFileProvider(
        PluginStaticWebAssets assets, Dictionary<string, byte[]> compressed, PluginStaticAssetSource source)
    {
        if (source == PluginStaticAssetSource.Memory && assets.Files is { } files)
        {
            return new MemoryFileProvider(compressed.Count == 0 ? files : new Dictionary<string, byte[]>([.. files, .. compressed]));
        }

        if (assets.Folder is { } folder)
        {
            return new PhysicalFileProvider(folder);
        }

        var written = PluginAssetFolder.Write(assets.Id, [.. assets.Files ?? new Dictionary<string, byte[]>(), .. compressed]);
        assets.Files = null;
        return new PhysicalFileProvider(written);
    }

    /// <summary>
    /// Reads the routes of the application's own static web assets, which <c>app.MapStaticAssets()</c> maps.
    /// </summary>
    private static HashSet<string> ReadHostRoutes(IWebHostEnvironment environment)
    {
        var routes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var path = Path.Combine(AppContext.BaseDirectory, $"{environment.ApplicationName}.staticwebassets.endpoints.json");
        if (File.Exists(path) && JsonNode.Parse(File.ReadAllText(path))?["Endpoints"] is JsonArray endpoints)
        {
            routes.UnionWith(endpoints.Select(endpoint => (string)endpoint!["Route"]!));
        }

        return routes;
    }
}
