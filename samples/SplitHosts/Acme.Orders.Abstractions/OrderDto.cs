namespace Acme.Orders.Abstractions;

/// <summary>
/// An order as the API returns it.
/// </summary>
public sealed record OrderDto(int Number, string Product, bool Shipped);

/// <summary>
/// Routes of the orders API, shared by server and client.
/// </summary>
public static class OrdersRoutes
{
    public const string Orders = "/api/orders";
}
