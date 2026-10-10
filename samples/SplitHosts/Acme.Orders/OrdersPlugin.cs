using System.Diagnostics;
using Acme.Orders.Abstractions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using OpenTelemetry.Trace;
using Plugwright;

namespace Acme.Orders;

#region plugin
/// <summary>
/// The backend part of the orders feature: services, API, health check and tracing.
/// </summary>
public sealed class OrdersPlugin : IPlugin, IWebPlugin
{
    internal static readonly ActivitySource s_activitySource = new("Acme.Orders");

    public void Configure(PluginContext context)
    {
        var services = context.Builder.Services;
        services.AddSingleton<OrderRepository>();

        // Appears under /health together with the host's own checks.
        services.AddHealthChecks().AddCheck<OrderRepositoryHealthCheck>("orders");

        // The plugin's own traces, exported like the host's.
        services.ConfigureOpenTelemetryTracerProvider(tracing => tracing.AddSource(s_activitySource.Name));
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints) =>
        endpoints.MapGet(OrdersRoutes.Orders, (OrderRepository orders) =>
        {
            using var activity = s_activitySource.StartActivity("List orders");
            return orders.GetAll();
        });
}
#endregion

internal sealed class OrderRepository
{
    private readonly OrderDto[] _orders = [new(1001, "Coffee beans", Shipped: true), new(1002, "Espresso machine", Shipped: false)];

    public IReadOnlyList<OrderDto> GetAll() => _orders;
}

internal sealed class OrderRepositoryHealthCheck(OrderRepository orders) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default) =>
        Task.FromResult(orders.GetAll().Count > 0
            ? HealthCheckResult.Healthy("Orders are available.")
            : HealthCheckResult.Degraded("No orders."));
}
