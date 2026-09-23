namespace Pbt205.Trading;

/// <summary>
/// Outstanding orders for one stock, and the matching rules.
///
/// Agreed rules (write these up in the report):
/// * A buy matches a sell when the buy price is greater than or equal to the sell price.
/// * Best price first: highest buy, lowest sell. Same price, earliest order first.
/// * The trade happens at the resting order's price (the one already in the book).
///
/// TODO (Task 2): implement Add() and the matching logic, then decide the data structure
/// (sorted list, priority queue or plain list) and justify the choice in the report.
/// </summary>
public sealed class OrderBook
{
    /// <summary>Adds an order and returns any trades it caused, newest last.</summary>
    public IReadOnlyList<TradeMessage> Add(OrderMessage order)
    {
        ArgumentNullException.ThrowIfNull(order);
        throw new NotImplementedException("Task 2: matching is not written yet.");
    }
}
