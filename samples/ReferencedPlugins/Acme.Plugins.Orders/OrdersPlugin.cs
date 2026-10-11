using Microsoft.Extensions.Logging;
using Plugwright;

namespace Acme.Plugins.Orders;

// --8<-- [start:plugin]
public sealed class OrdersPlugin : IPlugin
{
    public void Configure(PluginContext context) => context.Logger.LogInformation("Orders configured");
}
// --8<-- [end:plugin]
