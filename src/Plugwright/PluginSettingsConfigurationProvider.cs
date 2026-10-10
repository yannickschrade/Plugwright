using Microsoft.Extensions.Configuration.Json;

namespace Plugwright;

internal sealed class PluginSettingsConfigurationProvider(JsonConfigurationSource source) : JsonConfigurationProvider(source)
{
    private const string AllowedPrefix = PluginConfiguration.PluginsSectionPath + ":";

    public override void Load(Stream stream)
    {
        base.Load(stream);

        foreach (var key in Data.Keys.Where(key => !key.StartsWith(AllowedPrefix, StringComparison.OrdinalIgnoreCase)).ToList())
        {
            Data.Remove(key);
        }
    }
}
