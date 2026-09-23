using System.Globalization;

namespace Pbt205.Trading.Gui;

/// <summary>One row in the prices grid: the latest state of a single stock.</summary>
public sealed class StockRow(string symbol)
{
    public string Symbol { get; } = symbol;

    public string LastPrice { get; private set; } = "-";

    public string LastQuantity { get; private set; } = "-";

    public string LastTrade { get; private set; } = "-";

    public int Trades { get; private set; }

    public void Apply(TradeMessage trade)
    {
        LastPrice = trade.Price.ToString("0.00", CultureInfo.InvariantCulture);
        LastQuantity = trade.Quantity.ToString(CultureInfo.InvariantCulture);
        LastTrade = trade.TradedAt.ToLocalTime().ToString("HH:mm:ss");
        Trades++;
    }
}

/// <summary>One row in the recent trades grid.</summary>
public sealed class TradeRow(TradeMessage trade)
{
    public string Time { get; } = trade.TradedAt.ToLocalTime().ToString("HH:mm:ss");

    public string Symbol { get; } = trade.Symbol;

    public string Quantity { get; } = trade.Quantity.ToString(CultureInfo.InvariantCulture);

    public string Price { get; } = trade.Price.ToString("0.00", CultureInfo.InvariantCulture);

    public string Buyer { get; } = trade.Buyer;

    public string Seller { get; } = trade.Seller;
}

/// <summary>One row in the order book grid: an order still waiting for a match.</summary>
public sealed class BookRow(string symbol, string side, BookEntry entry)
{
    public string Symbol { get; } = symbol;

    public string Side { get; } = side;

    public string Trader { get; } = entry.Username;

    public string Quantity { get; } = entry.Quantity.ToString(CultureInfo.InvariantCulture);

    public string Price { get; } = entry.Price.ToString("0.00", CultureInfo.InvariantCulture);
}
