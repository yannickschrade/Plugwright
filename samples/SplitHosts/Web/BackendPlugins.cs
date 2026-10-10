using Plugwright;

namespace SplitHosts.Web;

// --8<-- [start:backend]
/// <summary>
/// Reads the plugin catalog of the API host once at startup and turns off UI plugins whose backend plugin is not loaded
/// there. The decision lasts until the next start, like every change to the plugins.
/// </summary>
internal static class BackendPlugins
{
    public static async Task TurnOffUiPluginsWithoutBackendAsync(WebApplicationBuilder builder, Dictionary<string, string> backendOfUiPlugin)
    {
        // Aspire passes the API's address as configuration. Service discovery itself is not available before Build().
        var apiAddress = builder.Configuration["services:api:https:0"] ?? builder.Configuration["services:api:http:0"];
        if (apiAddress is null)
        {
            return;
        }

        PluginStatus[] backendPlugins;
        try
        {
            using var http = new HttpClient { BaseAddress = new Uri(apiAddress), Timeout = TimeSpan.FromSeconds(10) };
            backendPlugins = await http.GetFromJsonAsync<PluginStatus[]>("/_plugwright/plugins") ?? [];
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            // The API is not reachable: keep the UI plugins; their pages show the API's error instead.
            Console.Error.WriteLine($"The plugin catalog of the API could not be read: {exception.Message}");
            return;
        }

        foreach ((var uiPlugin, var backendPlugin) in backendOfUiPlugin)
        {
            var loaded = backendPlugins.Any(plugin =>
                string.Equals(plugin.Id, backendPlugin, StringComparison.OrdinalIgnoreCase) && plugin.State == PluginState.Loaded);
            if (!loaded)
            {
                builder.Configuration[$"Plugwright:Plugins:{uiPlugin}:Enabled"] = "false";
            }
        }
    }
}
// --8<-- [end:backend]
