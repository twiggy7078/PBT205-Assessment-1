namespace Pbt205.Trading;

/// <summary>An order still waiting in the book, with the quantity that has not traded yet.</summary>
public sealed record RestingOrder(OrderMessage Order, int Remaining);

/// <summary>
/// Outstanding orders for one stock, and the rules for matching them.
///
/// Rules (also written up in docs/message-contracts.md):
/// * A buy matches a sell when the buy price is greater than or equal to the sell price.
/// * Best price first: highest buy, lowest sell. At the same price, the earliest order wins.
/// * The trade happens at the resting order's price, which is the one already in the book.
/// * An order that cannot be filled completely trades what it can and the rest waits in the book.
///
/// Data structure: one list per side, kept in matching order, so the best order is always at
/// index 0 and matching never has to search. With the handful of orders this prototype sees,
/// a list beats a heap because it stays readable and supports showing the whole book in a GUI.
/// </summary>
public sealed class OrderBook(string symbol)
{
    private readonly List<RestingOrder> _buys = [];   // highest price first
    private readonly List<RestingOrder> _sells = [];  // lowest price first

    public string Symbol { get; } = string.IsNullOrWhiteSpace(symbol)
        ? throw new ArgumentException("Symbol can't be empty.", nameof(symbol))
        : symbol.Trim().ToUpperInvariant();

    /// <summary>The price of the most recent trade, or null if nothing has traded yet.</summary>
    public decimal? LastTradedPrice { get; private set; }

    /// <summary>Highest price someone is willing to buy at.</summary>
    public decimal? BestBid => _buys.Count > 0 ? _buys[0].Order.Price : null;

    /// <summary>Lowest price someone is willing to sell at.</summary>
    public decimal? BestAsk => _sells.Count > 0 ? _sells[0].Order.Price : null;

    public IReadOnlyList<RestingOrder> Buys => _buys;

    public IReadOnlyList<RestingOrder> Sells => _sells;

    /// <summary>
    /// Adds an order to the book, matching it against the other side first.
    /// Returns the trades it caused, oldest first. An empty list means it is now resting in the book.
    /// </summary>
    public IReadOnlyList<TradeMessage> Add(OrderMessage order)
    {
        ArgumentNullException.ThrowIfNull(order);
        if (!string.Equals(order.Symbol, Symbol, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException($"Order is for {order.Symbol}, but this book is for {Symbol}.", nameof(order));
        }
        if (order.Quantity <= 0)
        {
            throw new ArgumentException("Quantity must be greater than zero.", nameof(order));
        }
        if (order.Price <= 0)
        {
            throw new ArgumentException("Price must be greater than zero.", nameof(order));
        }

        List<RestingOrder> opposite = order.Side == OrderSide.Buy ? _sells : _buys;
        List<RestingOrder> ownSide = order.Side == OrderSide.Buy ? _buys : _sells;

        var trades = new List<TradeMessage>();
        int remaining = order.Quantity;

        while (remaining > 0 && opposite.Count > 0 && IsAcceptable(order, opposite[0].Order))
        {
            RestingOrder best = opposite[0];
            int quantity = Math.Min(remaining, best.Remaining);
            decimal price = best.Order.Price; // the resting order's price

            trades.Add(new TradeMessage(
                TradeId: Guid.NewGuid(),
                Symbol: Symbol,
                Buyer: order.Side == OrderSide.Buy ? order.Username : best.Order.Username,
                Seller: order.Side == OrderSide.Buy ? best.Order.Username : order.Username,
                Quantity: quantity,
                Price: price,
                TradedAt: DateTimeOffset.UtcNow));

            LastTradedPrice = price;
            remaining -= quantity;

            if (best.Remaining == quantity)
            {
                opposite.RemoveAt(0);
            }
            else
            {
                opposite[0] = best with { Remaining = best.Remaining - quantity };
            }
        }

        if (remaining > 0)
        {
            Insert(ownSide, new RestingOrder(order, remaining), order.Side);
        }

        return trades;
    }

    /// <summary>The current state of the book, best prices first, for sending to other apps.</summary>
    public BookSnapshot Snapshot(int depth = 10)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(depth);
        return new BookSnapshot(
            Symbol,
            Bids: [.. _buys.Take(depth).Select(ToEntry)],
            Asks: [.. _sells.Take(depth).Select(ToEntry)],
            LastTradedPrice,
            DateTimeOffset.UtcNow);

        static BookEntry ToEntry(RestingOrder resting) =>
            new(resting.Order.Username, resting.Remaining, resting.Order.Price);
    }

    private static bool IsAcceptable(OrderMessage incoming, OrderMessage resting) =>
        incoming.Side == OrderSide.Buy
            ? incoming.Price >= resting.Price
            : incoming.Price <= resting.Price;

    /// <summary>Puts an order in its place: best price first, and behind anything already at that price.</summary>
    private static void Insert(List<RestingOrder> side, RestingOrder resting, OrderSide sideKind)
    {
        int index = 0;
        while (index < side.Count && !IsBetter(resting.Order, side[index].Order, sideKind))
        {
            index++;
        }
        side.Insert(index, resting);
    }

    private static bool IsBetter(OrderMessage candidate, OrderMessage existing, OrderSide side)
    {
        int comparison = side == OrderSide.Buy
            ? candidate.Price.CompareTo(existing.Price)     // buys: higher price is better
            : existing.Price.CompareTo(candidate.Price);    // sells: lower price is better

        // Same price: whoever was placed first stays ahead.
        return comparison > 0 || (comparison == 0 && candidate.PlacedAt < existing.PlacedAt);
    }
}
