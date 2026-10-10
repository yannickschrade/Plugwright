using System.Net.Http.Json;
using Acme.Orders.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Plugwright;
using Plugwright.AspNetCore;

namespace Acme.Orders.Ui;

// --8<-- [start:plugin]
/// <summary>
/// The UI part of the orders feature, plugin ID Acme.Orders.Ui. It calls the backend part through the API.
/// </summary>
public sealed class OrdersUiPlugin : IPlugin, IBlazorPlugin
{
    public void Configure(PluginContext context)
    {
        // "https+http://api" is resolved by Aspire service discovery. Hosts without Aspire set ApiAddress.
        var apiAddress = context.Configuration["ApiAddress"] ?? "https+http://api";
        context.Builder.Services.AddHttpClient<OrdersClient>(client => client.BaseAddress = new Uri(apiAddress));
    }
}
// --8<-- [end:plugin]

public sealed class OrdersClient(HttpClient http)
{
    public async Task<IReadOnlyList<OrderDto>> GetOrdersAsync(CancellationToken cancellationToken = default) =>
        await http.GetFromJsonAsync<OrderDto[]>(OrdersRoutes.Orders, cancellationToken) ?? [];
}
