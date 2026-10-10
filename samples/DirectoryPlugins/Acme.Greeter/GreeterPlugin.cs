using Microsoft.Extensions.DependencyInjection;
using Plugwright;

namespace Acme.Greeter;

// --8<-- [start:plugin]
public sealed class GreeterPlugin : IPlugin
{
    public void Configure(PluginContext context)
    {
        context.Builder.Services.AddOptions<GreeterOptions>().Bind(context.Configuration);
        context.Builder.Services.AddHostedService<GreeterService>();
    }
}
// --8<-- [end:plugin]
