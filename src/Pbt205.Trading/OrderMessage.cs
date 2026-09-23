namespace Pbt205.Trading;

public enum OrderSide
{
    Buy,
    Sell,
}

/// <summary>
/// An order sent to the "orders" topic. Routing key is the stock symbol, e.g. "XYZ".
/// { "orderId": "...", "username": "Andrew", "symbol": "XYZ", "side": "Buy",
///   "quantity": 100, "price": 10.5, "placedAt": "2026-09-23T04:00:00+00:00" }
/// </summary>
public sealed record OrderMessage(
    Guid OrderId,
    string Username,
    string Symbol,
    OrderSide Side,
    int Quantity,
    decimal Price,
    DateTimeOffset PlacedAt);
