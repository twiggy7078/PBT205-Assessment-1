// PBT205 Assessment 1, Task 2: the exchange.
//
// Usage: exchange <endpoint> [--symbol XYZ | --all]
//
// Subscribes to the "orders" topic, matches orders against the order book,
// and publishes completed trades to the "trades" topic.
// There should only ever be one exchange running against a given RabbitMQ.

using System.Globalization;
using Pbt205.Messaging;
using Pbt205.Trading;

if (args.Any(a => a is "-h" or "--help" or "/?"))
{
    PrintUsage();
    return 0;
}

if (!TryParseArguments(args, out string endpointText, out string bindingKey, out string? argumentError))
{
    Console.Error.WriteLine(argumentError);
    PrintUsage();
    return 1;
}

if (!BrokerEndpoint.TryParse(endpointText, out BrokerEndpoint? endpoint, out string endpointError))
{
    Console.Error.WriteLine(endpointError);
    return 1;
}

BrokerCredentials? credentials = BrokerCredentials.FromEnvironmentOrPrompt();
if (credentials is null)
{
    Console.Error.WriteLine(
        $"No RabbitMQ login. Set {BrokerCredentials.UserVariable} and {BrokerCredentials.PasswordVariable}, " +
        "or run the app in a console so it can ask.");
    return 1;
}

var books = new Dictionary<string, OrderBook>(StringComparer.OrdinalIgnoreCase);
var bookLock = new SemaphoreSlim(1, 1);

using var shutdown = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    shutdown.Cancel();
};

try
{
    await using MessageBus bus = await MessageBus.ConnectAsync(endpoint, credentials, "exchange", shutdown.Token);
    bus.StatusChanged += message => Console.WriteLine($"* {message}");

    await bus.SubscribeAsync<OrderMessage>(
        Topics.Orders,
        bindingKey,
        (order, context) => HandleOrderAsync(bus, books, bookLock, order, context, shutdown.Token),
        ex => Console.Error.WriteLine($"! {ex.Message}"),
        shutdown.Token);

    Console.WriteLine($"* Exchange connected to {endpoint}, watching orders for {(bindingKey == "#" ? "all stocks" : bindingKey)}.");
    Console.WriteLine("* Press Ctrl+C to stop.");

    // Nothing else to do on this thread: work happens as orders arrive.
    await Task.Delay(Timeout.Infinite, shutdown.Token);
    return 0;
}
catch (OperationCanceledException)
{
    Console.WriteLine("* Exchange stopped.");
    return 0;
}
catch (MessageBusException ex)
{
    Console.Error.WriteLine(ex.Message);
    return 2;
}

static async Task HandleOrderAsync(
    MessageBus bus,
    Dictionary<string, OrderBook> books,
    SemaphoreSlim bookLock,
    OrderMessage order,
    MessageContext context,
    CancellationToken cancellationToken)
{
    if (string.IsNullOrWhiteSpace(order.Username) || string.IsNullOrWhiteSpace(order.Symbol)
        || order.Quantity <= 0 || order.Price <= 0m)
    {
        Console.Error.WriteLine($"! Ignored an incomplete order on '{context.RoutingKey}'.");
        return;
    }

    IReadOnlyList<TradeMessage> trades;
    string bookState;

    // One order at a time, so the book can't be changed from under us.
    await bookLock.WaitAsync(cancellationToken).ConfigureAwait(false);
    try
    {
        string symbol = TradingNames.NormaliseSymbol(order.Symbol);
        if (!books.TryGetValue(symbol, out OrderBook? book))
        {
            book = new OrderBook(symbol);
            books[symbol] = book;
        }

        Console.WriteLine(
            $"> {order.Side.ToString().ToUpperInvariant()} {order.Quantity} {symbol} @ {Money(order.Price)} from {order.Username}");

        trades = book.Add(order);
        bookState = $"  book {symbol}: bid {Money(book.BestBid)} / ask {Money(book.BestAsk)}, " +
                    $"{book.Buys.Count} buys, {book.Sells.Count} sells";
    }
    catch (ArgumentException ex)
    {
        Console.Error.WriteLine($"! Rejected an order: {ex.Message}");
        return;
    }
    finally
    {
        bookLock.Release();
    }

    foreach (TradeMessage trade in trades)
    {
        // Publish first, print second, so a failure to publish is obvious.
        await bus.PublishAsync(Topics.Trades, trade.Symbol, trade, cancellationToken).ConfigureAwait(false);
        Console.WriteLine(
            $"= TRADE {trade.Quantity} {trade.Symbol} @ {Money(trade.Price)}: {trade.Buyer} bought from {trade.Seller}");
    }

    if (trades.Count == 0)
    {
        Console.WriteLine("  no match, resting in the book");
    }
    Console.WriteLine(bookState);
}

static string Money(decimal? value) =>
    value is null ? "-" : value.Value.ToString("0.00", CultureInfo.InvariantCulture);

static bool TryParseArguments(
    string[] args, out string endpoint, out string bindingKey,
    [System.Diagnostics.CodeAnalysis.NotNullWhen(false)] out string? error)
{
    endpoint = string.Empty;
    bindingKey = "#"; // every stock
    error = null;
    var positional = new List<string>();

    for (int i = 0; i < args.Length; i++)
    {
        if (args[i] is "--symbol" or "-s")
        {
            if (i + 1 >= args.Length)
            {
                error = "--symbol needs a stock symbol after it.";
                return false;
            }
            string symbol = args[++i];
            string? symbolError = TradingNames.ValidateSymbol(symbol);
            if (symbolError is not null)
            {
                error = symbolError;
                return false;
            }
            bindingKey = TradingNames.NormaliseSymbol(symbol);
        }
        else if (args[i] == "--all")
        {
            bindingKey = "#";
        }
        else if (args[i].StartsWith('-'))
        {
            error = $"Unknown option '{args[i]}'.";
            return false;
        }
        else
        {
            positional.Add(args[i]);
        }
    }

    if (positional.Count != 1)
    {
        error = "Expected a middleware endpoint.";
        return false;
    }

    endpoint = positional[0];
    return true;
}

static void PrintUsage()
{
    Console.WriteLine($"""

        Usage: exchange <endpoint> [--symbol XYZ | --all]

          <endpoint>    RabbitMQ endpoint: 5672, host:5672 or amqp://host:5672
          --symbol, -s  Handle one stock only
          --all         Handle every stock (default)

        RabbitMQ login: set PBT205_RABBITMQ_USER and PBT205_RABBITMQ_PASSWORD,
        or the app will ask for them.

        Example: exchange 100.74.122.113:5672
        """);
}
