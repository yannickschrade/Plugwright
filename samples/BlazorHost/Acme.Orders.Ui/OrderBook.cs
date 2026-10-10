namespace Acme.Orders.Ui;

/// <summary>
/// Orders in memory: the UI host of this sample has no database and no API.
/// </summary>
public sealed class OrderBook
{
    private readonly List<Order> _orders = [new(1001, "Coffee beans", Shipped: true), new(1002, "Espresso machine", Shipped: false)];

    public IReadOnlyList<Order> Orders
    {
        get
        {
            lock (_orders)
            {
                return [.. _orders];
            }
        }
    }

    public void Ship(int number)
    {
        lock (_orders)
        {
            var index = _orders.FindIndex(order => order.Number == number);
            _orders[index] = _orders[index] with { Shipped = true };
        }
    }
}

public sealed record Order(int Number, string Product, bool Shipped);
