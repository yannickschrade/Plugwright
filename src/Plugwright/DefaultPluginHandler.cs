using Microsoft.Extensions.Hosting;

namespace Plugwright;

/// <summary>
/// The built-in handler: calls <see cref="IPlugin.Configure"/> for every plugin that implements <see cref="IPlugin"/>.
/// </summary>
internal sealed class DefaultPluginHandler : PluginHandler<IPlugin>
{
    protected override void Apply(IPlugin instance, PluginDescriptor plugin, IHostApplicationBuilder builder)
    {
        instance.Configure(plugin.Context);
    }
}
