using Microsoft.Extensions.DependencyInjection;
using Plugwright;

namespace Heartbeat;

// --8<-- [start:plugin]
public sealed class HeartbeatPlugin : IPlugin
{
    public void Configure(PluginContext context)
    {
        context.Builder.Services
            .AddOptions<HeartbeatOptions>()
            .Bind(context.Configuration)
            .Validate(options => options.Interval > TimeSpan.Zero, "Interval must be positive.")
            .ValidateOnStart();

        context.Builder.Services.AddHostedService<HeartbeatService>();
    }
}
// --8<-- [end:plugin]
