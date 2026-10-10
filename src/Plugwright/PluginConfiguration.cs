using Microsoft.Extensions.Configuration;

namespace Plugwright;

/// <summary>
/// Configuration paths used by the plugin system.
/// </summary>
internal static class PluginConfiguration
{
    public const string PluginsSectionPath = "Plugwright:Plugins";

    public static string SettingsSectionPath(string pluginId) => $"{PluginsSectionPath}:{pluginId}:Settings";

    /// <summary>
    /// Gets the key that turns a plugin on or off.
    /// </summary>
    public static string EnabledKey(string pluginId) => $"{PluginsSectionPath}:{pluginId}:Enabled";

    /// <summary>
    /// Returns whether the configuration turns a plugin off. A missing value means on.
    /// </summary>
    /// <exception cref="PluginException">The value is neither <c>true</c> nor <c>false</c>.</exception>
    public static bool IsDisabled(IConfiguration configuration, string pluginId)
    {
        var key = EnabledKey(pluginId);
        return configuration[key] switch
        {
            null or "" => false,
            var value when bool.TryParse(value, out var enabled) => !enabled,
            var value => throw new PluginException(
                $"The configuration '{key}' must be true or false, but is '{value}'.", pluginId, innerException: null),
        };
    }

    /// <summary>
    /// Returns the reason shown for a plugin that is turned off.
    /// </summary>
    public static string DisabledReason(string pluginId) => $"It is turned off in configuration ({EnabledKey(pluginId)}).";
}
