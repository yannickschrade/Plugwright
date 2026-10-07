using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Plugwright;

namespace CustomHandler;

#region handler
public sealed class GreetingPluginHandler : IPluginHandler
{
    public bool CanHandle(Type pluginType) => typeof(IGreetingPlugin).IsAssignableFrom(pluginType);

    // Phase 1: register what the plugin contributes.
    public void Apply(PluginDescriptor plugin, IHostApplicationBuilder builder)
    {
        foreach (var text in ((IGreetingPlugin)plugin.Instance).GetGreetings())
        {
            builder.Services.AddSingleton(new Greeting(text, plugin.Metadata.Id));
        }
    }

    // Phase 2: work with the built host.
    public void Use(PluginDescriptor plugin, IHost app)
    {
        var logger = app.Services.GetRequiredService<ILogger<GreetingPluginHandler>>();
        logger.LogInformation("Greetings from {PluginType} are ready", plugin.PluginType.Name);
    }
}
#endregion
