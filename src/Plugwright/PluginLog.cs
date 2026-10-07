using Microsoft.Extensions.Logging;

namespace Plugwright;

/// <summary>
/// Log events of the plugin system. Event IDs are part of the documented behavior and must not change.
/// </summary>
internal static partial class PluginLog
{
    [LoggerMessage(EventId = 1000, EventName = "PluginLoaded", Level = LogLevel.Information,
        Message = "Plugin {PluginId} {PluginVersion} loaded ({PluginType}, {PluginSource})")]
    public static partial void PluginLoaded(ILogger logger, string pluginId, string pluginVersion, string? pluginType, string pluginSource);

    [LoggerMessage(EventId = 1001, EventName = "PluginSkipped", Level = LogLevel.Error,
        Message = "Plugin {PluginId} ({PluginSource}) skipped: {Reason}")]
    public static partial void PluginSkipped(ILogger logger, Exception? exception, string pluginId, string pluginSource, string reason);

    [LoggerMessage(EventId = 1002, EventName = "PluginDirectoryNotFound", Level = LogLevel.Warning,
        Message = "Plugin directory {PluginDirectory} does not exist. No plugins are loaded from it.")]
    public static partial void PluginDirectoryNotFound(ILogger logger, string pluginDirectory);

    [LoggerMessage(EventId = 1003, EventName = "AssemblyOutsidePluginFolder", Level = LogLevel.Warning,
        Message = "Assembly {AssemblyPath} is ignored. Each plugin needs its own folder in {PluginDirectory}, for example {PluginDirectory}/Acme.Heartbeat/Acme.Heartbeat.dll.")]
    public static partial void AssemblyOutsidePluginFolder(ILogger logger, string assemblyPath, string pluginDirectory);
}
