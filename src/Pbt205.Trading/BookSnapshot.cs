namespace Pbt205.Trading;

/// <summary>One order still waiting in the book, as sent to other apps.</summary>
public sealed record BookEntry(string Username, int Quantity, decimal Price);

/// <summary>
/// The state of one stock's order book after an order was handled, published to the "book" topic.
///
/// This goes beyond what the brief asks for. It exists so the trading window can show orders that
/// are waiting, not only completed trades, which makes the system much easier to demonstrate.
/// </summary>
public sealed record BookSnapshot(
    string Symbol,
    IReadOnlyList<BookEntry> Bids,
    IReadOnlyList<BookEntry> Asks,
    decimal? LastTradedPrice,
    DateTimeOffset AsOf);
