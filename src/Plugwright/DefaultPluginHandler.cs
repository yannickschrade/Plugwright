using Microsoft.Extensions.Hosting;

namespace Plugwright;

/// <summary>
/// The built-in handler: calls <see cref="IPlugin.Configure"/> for every plugin that implements <see cref="IPlugin"/>.
/// </summary>
internal sealed class DefaultPluginHandler : IPluginHandler
{
    public bool CanHandle(Type pluginType) => typeof(IPlugin).IsAssignableFrom(pluginType);

    public void Apply(PluginDescriptor plugin, IHostApplicationBuilder builder)
    {
        ((IPlugin)plugin.Instance).Configure(plugin.Context);
    }
}
