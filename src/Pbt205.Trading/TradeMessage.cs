namespace Pbt205.Trading;

/// <summary>
/// A completed trade published to the "trades" topic. Routing key is the stock symbol.
/// The price is the resting order's price (the order that was already in the book).
/// </summary>
public sealed record TradeMessage(
    Guid TradeId,
    string Symbol,
    string Buyer,
    string Seller,
    int Quantity,
    decimal Price,
    DateTimeOffset TradedAt);
