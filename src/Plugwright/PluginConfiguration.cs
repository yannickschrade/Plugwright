namespace Plugwright;

/// <summary>
/// Configuration paths used by the plugin system.
/// </summary>
internal static class PluginConfiguration
{
    public const string PluginsSectionPath = "Plugwright:Plugins";

    public static string SettingsSectionPath(string pluginId) => $"{PluginsSectionPath}:{pluginId}:Settings";
}
