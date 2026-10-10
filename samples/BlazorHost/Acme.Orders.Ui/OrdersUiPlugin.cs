using BlazorHost.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Plugwright;

namespace Acme.Orders.Ui;

// --8<-- [start:plugin]
/// <summary>
/// Brings the page /orders. Its menu entry uses the host's own contract, <see cref="NavigationItem"/>.
/// </summary>
public sealed class OrdersUiPlugin : IPlugin, IBlazorPlugin
{
    public void Configure(PluginContext context)
    {
        context.Builder.Services.AddSingleton(new NavigationItem("Orders", "orders"));
        context.Builder.Services.AddSingleton<OrderBook>();
    }
}
// --8<-- [end:plugin]
