using Microsoft.Extensions.Logging;

namespace Plugwright;

/// <summary>
/// Log events of the plugin system. Event IDs are part of the documented behavior and must not change.
/// </summary>
internal static partial class PluginLog
{
    [LoggerMessage(EventId = 1000, EventName = "PluginLoaded", Level = LogLevel.Information,
        Message = "Plugin {PluginId} {PluginVersion} loaded ({PluginType})")]
    public static partial void PluginLoaded(ILogger logger, string pluginId, string pluginVersion, string? pluginType);

    [LoggerMessage(EventId = 1001, EventName = "PluginSkipped", Level = LogLevel.Error,
        Message = "Plugin {PluginId} ({PluginType}) skipped: {Reason}")]
    public static partial void PluginSkipped(ILogger logger, Exception? exception, string pluginId, string? pluginType, string reason);
}
