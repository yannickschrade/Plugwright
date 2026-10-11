using Microsoft.Extensions.Logging;

namespace Plugwright.AspNetCore;

internal static partial class PluginStaticAssetsLog
{
    [LoggerMessage(EventId = 1014, EventName = "PluginStaticAssetsNotPublished", Level = LogLevel.Warning,
        Message = "The static web assets of plugin {PluginId} are not served: its folder contains build output. Deploy the plugin with dotnet publish")]
    public static partial void NotPublished(ILogger logger, string pluginId);

    [LoggerMessage(EventId = 1015, EventName = "PluginStaticAssetSkipped", Level = LogLevel.Debug,
        Message = "Static web asset {Route} of {Id} is not served, because the application or another plugin already serves it")]
    public static partial void RouteSkipped(ILogger logger, string id, string route);
}
