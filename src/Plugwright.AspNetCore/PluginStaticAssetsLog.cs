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

    [LoggerMessage(EventId = 1016, EventName = "PluginStaticAssetsServedByApplication", Level = LogLevel.Warning,
        Message = "The application serves {Count} static web assets of plugin {PluginId} itself, so the plugin's own files are not used. The application probably references the plugin project or package; remove the reference")]
    public static partial void ServedByApplication(ILogger logger, string pluginId, int count);
}
