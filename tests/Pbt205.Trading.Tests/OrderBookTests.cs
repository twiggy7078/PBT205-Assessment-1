namespace Pbt205.Trading.Tests;

public class OrderBookTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 21, 0, 0, 0, TimeSpan.Zero);
    private int _sequence;

    // Each order is placed one second after the previous one, so arrival order is clear.
    private OrderMessage Order(string username, OrderSide side, int quantity, decimal price, string symbol = "XYZ") =>
        new(Guid.NewGuid(), username, symbol, side, quantity, price, Start.AddSeconds(_sequence++));

    private OrderMessage Buy(string username, int quantity, decimal price) => Order(username, OrderSide.Buy, quantity, price);

    private OrderMessage Sell(string username, int quantity, decimal price) => Order(username, OrderSide.Sell, quantity, price);

    // Resting orders

    [Fact]
    public void BuyWithNoSellersRestsInBook()
    {
        var book = new OrderBook("XYZ");
        Assert.Empty(book.Add(Buy("Jane", 100, 10.00m)));
        Assert.Single(book.Buys);
        Assert.Empty(book.Sells);
    }

    [Fact]
    public void SellWithNoBuyersRestsInBook()
    {
        var book = new OrderBook("XYZ");
        Assert.Empty(book.Add(Sell("Andrew", 100, 10.00m)));
        Assert.Single(book.Sells);
        Assert.Empty(book.Buys);
    }

    // Best bid and offer

    [Fact]
    public void EmptyBookHasNoBestPricesOrLastPrice()
    {
        var book = new OrderBook("XYZ");
        Assert.Null(book.BestBid);
        Assert.Null(book.BestAsk);
        Assert.Null(book.LastTradedPrice);
    }

    [Fact]
    public void BestBidIsHighestBuy()
    {
        var book = new OrderBook("XYZ");
        book.Add(Buy("Jane", 100, 9.50m));
        book.Add(Buy("Angelo", 100, 10.25m));
        book.Add(Buy("Jacobo", 100, 9.75m));
        Assert.Equal(10.25m, book.BestBid);
    }

    [Fact]
    public void BestAskIsLowestSell()
    {
        var book = new OrderBook("XYZ");
        book.Add(Sell("Jane", 100, 10.50m));
        book.Add(Sell("Andrew", 100, 10.00m));
        book.Add(Sell("Jacobo", 100, 10.25m));
        Assert.Equal(10.00m, book.BestAsk);
    }

    // Crossing in both directions

    [Fact]
    public void BuyAtOrAboveBestAskTrades()
    {
        var book = new OrderBook("XYZ");
        book.Add(Sell("Andrew", 100, 10.00m));
        TradeMessage trade = Assert.Single(book.Add(Buy("Jane", 100, 10.25m)));
        Assert.Equal("Jane", trade.Buyer);
        Assert.Equal("Andrew", trade.Seller);
        Assert.Empty(book.Buys);
        Assert.Empty(book.Sells);
    }

    [Fact]
    public void SellAtOrBelowBestBidTrades()
    {
        var book = new OrderBook("XYZ");
        book.Add(Buy("Jane", 100, 10.25m));
        TradeMessage trade = Assert.Single(book.Add(Sell("Andrew", 100, 10.00m)));
        Assert.Equal("Jane", trade.Buyer);
        Assert.Equal("Andrew", trade.Seller);
        Assert.Empty(book.Buys);
        Assert.Empty(book.Sells);
    }

    [Fact]
    public void EqualBuyAndSellPricesTrade()
    {
        var book = new OrderBook("XYZ");
        book.Add(Sell("Andrew", 100, 10.00m));
        Assert.Single(book.Add(Buy("Jane", 100, 10.00m)));
    }

    [Fact]
    public void BuyBelowBestAskDoesNotTrade()
    {
        var book = new OrderBook("XYZ");
        book.Add(Sell("Andrew", 100, 10.50m));
        Assert.Empty(book.Add(Buy("Jane", 100, 10.25m)));
        Assert.Equal(10.25m, book.BestBid);
        Assert.Equal(10.50m, book.BestAsk);
    }

    // The trade happens at the resting order's price

    [Fact]
    public void IncomingBuyTradesAtRestingSellPrice()
    {
        var book = new OrderBook("XYZ");
        book.Add(Sell("Andrew", 100, 10.00m));
        Assert.Equal(10.00m, Assert.Single(book.Add(Buy("Jane", 100, 10.25m))).Price);
    }

    [Fact]
    public void IncomingSellTradesAtRestingBuyPrice()
    {
        var book = new OrderBook("XYZ");
        book.Add(Buy("Jane", 100, 10.25m));
        Assert.Equal(10.25m, Assert.Single(book.Add(Sell("Andrew", 100, 10.00m))).Price);
    }

    [Fact]
    public void TradeUpdatesLastTradedPrice()
    {
        var book = new OrderBook("XYZ");
        book.Add(Sell("Andrew", 100, 10.00m));
        book.Add(Buy("Jane", 100, 10.25m));
        Assert.Equal(10.00m, book.LastTradedPrice);
    }

    // Price priority

    [Fact]
    public void BuyMatchesLowestSellFirst()
    {
        var book = new OrderBook("XYZ");
        book.Add(Sell("Jane", 100, 10.50m));
        book.Add(Sell("Andrew", 100, 10.00m));
        TradeMessage trade = Assert.Single(book.Add(Buy("Angelo", 100, 11.00m)));
        Assert.Equal("Andrew", trade.Seller);
        Assert.Equal(10.50m, book.BestAsk);
    }

    [Fact]
    public void SellMatchesHighestBuyFirst()
    {
        var book = new OrderBook("XYZ");
        book.Add(Buy("Jane", 100, 9.50m));
        book.Add(Buy("Angelo", 100, 10.25m));
        TradeMessage trade = Assert.Single(book.Add(Sell("Jacobo", 100, 9.00m)));
        Assert.Equal("Angelo", trade.Buyer);
        Assert.Equal(9.50m, book.BestBid);
    }

    // Arrival priority at the same price

    [Fact]
    public void AtEqualPriceEarlierOrderTradesFirst()
    {
        var book = new OrderBook("XYZ");
        book.Add(Sell("Jane", 100, 10.00m));
        book.Add(Sell("Andrew", 100, 10.00m));
        TradeMessage trade = Assert.Single(book.Add(Buy("Angelo", 100, 10.00m)));
        Assert.Equal("Jane", trade.Seller);
        Assert.Equal("Andrew", Assert.Single(book.Sells).Order.Username);
    }

    // Partial fills

    [Fact]
    public void RemainderOfLargerOrderRestsAtItsOwnPrice()
    {
        // The integration run described in the report.
        var book = new OrderBook("XYZ");
        book.Add(Sell("Andrew", 100, 10.00m));
        book.Add(Sell("Jane", 100, 10.50m));
        book.Add(Buy("Jacobo", 100, 9.50m));
        TradeMessage trade = Assert.Single(book.Add(Buy("Angelo", 250, 10.25m)));

        Assert.Equal(100, trade.Quantity);
        Assert.Equal(10.00m, trade.Price);
        Assert.Equal(10.25m, book.BestBid);
        Assert.Equal(150, book.Buys[0].Remaining);
        Assert.Equal(9.50m, book.Buys[1].Order.Price);
        Assert.Equal(10.50m, book.BestAsk);
    }

    [Fact]
    public void LargeOrderTradesAcrossSeveralRestingOrders()
    {
        var book = new OrderBook("XYZ");
        book.Add(Buy("Jane", 100, 10.25m));
        book.Add(Buy("Angelo", 100, 9.50m));
        IReadOnlyList<TradeMessage> trades = book.Add(Sell("Jacobo", 300, 9.00m));

        Assert.Equal(2, trades.Count);
        Assert.Equal(10.25m, trades[0].Price);
        Assert.Equal(9.50m, trades[1].Price);
        Assert.Empty(book.Buys);
        RestingOrder rest = Assert.Single(book.Sells);
        Assert.Equal(100, rest.Remaining);
        Assert.Equal(9.00m, rest.Order.Price);
    }

    [Fact]
    public void PartlyFilledRestingOrderKeepsItsRemainder()
    {
        var book = new OrderBook("XYZ");
        book.Add(Sell("Andrew", 300, 10.00m));
        TradeMessage trade = Assert.Single(book.Add(Buy("Jane", 100, 10.00m)));

        Assert.Equal(100, trade.Quantity);
        Assert.Equal(200, Assert.Single(book.Sells).Remaining);
    }

    // Symbols and validation

    [Fact]
    public void SymbolsAreCaseInsensitive()
    {
        var book = new OrderBook(" xyz ");
        Assert.Equal("XYZ", book.Symbol);
        book.Add(Order("Jane", OrderSide.Buy, 100, 10.00m, symbol: "Xyz"));
        Assert.Equal(10.00m, book.BestBid);
        Assert.Equal("ACME", TradingNames.NormaliseSymbol("acme"));
    }

    [Fact]
    public void InvalidQuantityPriceOrSymbolIsRejectedByTheBook()
    {
        var book = new OrderBook("XYZ");
        Assert.Throws<ArgumentException>(() => book.Add(Buy("Jane", 0, 10.00m)));
        Assert.Throws<ArgumentException>(() => book.Add(Buy("Jane", 100, -1.00m)));
        Assert.Throws<ArgumentException>(() => book.Add(Order("Jane", OrderSide.Buy, 100, 10.00m, symbol: "ACME")));
        Assert.Empty(book.Buys);
    }

    [Fact]
    public void InvalidSymbolsAreRejected()
    {
        Assert.Null(TradingNames.ValidateSymbol("ACME"));
        Assert.NotNull(TradingNames.ValidateSymbol(""));
        Assert.NotNull(TradingNames.ValidateSymbol("XY.Z"));
        Assert.NotNull(TradingNames.ValidateSymbol("XYZ#"));
        Assert.NotNull(TradingNames.ValidateSymbol(new string('A', TradingNames.MaxSymbolLength + 1)));
    }
}
