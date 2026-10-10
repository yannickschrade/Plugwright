using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;

namespace Plugwright;

/// <summary>
/// The application-managed plugin settings file as a configuration source. It only reads keys under
/// <c>Plugwright:Plugins</c>, so that a file the application can write cannot change any other setting.
/// </summary>
internal sealed class PluginSettingsConfigurationSource : JsonConfigurationSource
{
    public PluginSettingsConfigurationSource(string fullPath)
    {
        Path = fullPath;
        Optional = true;
        ReloadOnChange = true;
        ResolveFileProvider();
    }

    public override IConfigurationProvider Build(IConfigurationBuilder builder)
    {
        EnsureDefaults(builder);
        return new PluginSettingsConfigurationProvider(this);
    }
}
