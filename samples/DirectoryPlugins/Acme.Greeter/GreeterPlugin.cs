using Microsoft.Extensions.DependencyInjection;
using Plugwright;

namespace Acme.Greeter;

#region plugin
public sealed class GreeterPlugin : IPlugin
{
    public void Configure(PluginContext context)
    {
        context.Builder.Services.AddOptions<GreeterOptions>().Bind(context.Configuration);
        context.Builder.Services.AddHostedService<GreeterService>();
    }
}
#endregion
