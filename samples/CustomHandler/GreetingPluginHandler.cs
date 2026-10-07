using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Plugwright;

namespace CustomHandler;

#region handler
public sealed class GreetingPluginHandler : PluginHandler<IGreetingPlugin>
{
    // Phase 1: register what the plugin contributes.
    protected override void Apply(IGreetingPlugin instance, PluginDescriptor plugin, IHostApplicationBuilder builder)
    {
        foreach (var text in instance.GetGreetings())
        {
            builder.Services.AddSingleton(new Greeting(text, plugin.Metadata.Id));
        }
    }

    // Phase 2 (optional): work with the built host.
    protected override void Use(IGreetingPlugin instance, PluginDescriptor plugin, IHost app)
    {
        var logger = app.Services.GetRequiredService<ILogger<GreetingPluginHandler>>();
        logger.LogInformation("Greetings from {PluginType} are ready", plugin.PluginType.Name);
    }
}
#endregion
