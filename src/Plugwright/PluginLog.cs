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

    [LoggerMessage(EventId = 1004, EventName = "PluginVerificationDisabled", Level = LogLevel.Information,
        Message = "No plugin verifiers are registered. Plugins from directories and packages are loaded without verification.")]
    public static partial void PluginVerificationDisabled(ILogger logger);

    [LoggerMessage(EventId = 1005, EventName = "PluginVerified", Level = LogLevel.Information,
        Message = "Plugin {PluginName} ({PluginLocation}) passed {Verifier}: {Result}")]
    public static partial void PluginVerified(ILogger logger, string pluginName, string pluginLocation, string verifier, string result);

    [LoggerMessage(EventId = 1006, EventName = "PluginVerificationFailed", Level = LogLevel.Error,
        Message = "Plugin {PluginName} ({PluginLocation}) failed {Verifier}: {Result}")]
    public static partial void PluginVerificationFailed(ILogger logger, Exception? exception, string pluginName, string pluginLocation, string verifier, string result);

    [LoggerMessage(EventId = 1007, EventName = "PackageDownloaded", Level = LogLevel.Information,
        Message = "Package {PackageId} {PackageVersion} downloaded from {Feed} to {PackagePath}")]
    public static partial void PackageDownloaded(ILogger logger, string packageId, string packageVersion, string feed, string packagePath);

    [LoggerMessage(EventId = 1008, EventName = "PluginStatus",
        Message = "Plugin {PluginId} {PluginVersion} ({PluginSource}): {PluginState}{Reason}")]
    private static partial void PluginStatus(
        ILogger logger, LogLevel level, string pluginId, string? pluginVersion, string pluginSource, PluginState pluginState, string reason);

    /// <summary>
    /// Logs the state of one plugin at startup: information if it is loaded or disabled, otherwise a warning.
    /// </summary>
    public static void PluginStatus(ILogger logger, PluginInfo plugin) =>
        PluginStatus(
            logger,
            plugin.State is PluginState.Loaded or PluginState.Disabled ? LogLevel.Information : LogLevel.Warning,
            plugin.Id,
            plugin.Version,
            plugin.Source,
            plugin.State,
            plugin.Reason is null ? "" : $". {plugin.Reason}");
}
