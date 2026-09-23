// PBT205 Assessment 1, Task 2: submit one order and exit.
//
// Usage: sendOrder <username> <endpoint> <BUY|SELL> <quantity> <price> [--symbol XYZ]
//
// The RabbitMQ login comes from PBT205_RABBITMQ_USER and PBT205_RABBITMQ_PASSWORD, or is asked for at start-up.

using System.Globalization;
using Pbt205.Messaging;
using Pbt205.Trading;

if (args.Any(a => a is "-h" or "--help" or "/?"))
{
    PrintUsage();
    return 0;
}

if (!TryParseArguments(args, out string username, out string endpointText, out OrderSide side,
        out int quantity, out decimal price, out string symbol, out string? argumentError))
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

var order = new OrderMessage(
    OrderId: Guid.NewGuid(),
    Username: username,
    Symbol: symbol,
    Side: side,
    Quantity: quantity,
    Price: price,
    PlacedAt: DateTimeOffset.UtcNow);

try
{
    // Connect, publish one order, exit. The exchange does the matching.
    await using MessageBus bus = await MessageBus.ConnectAsync(endpoint, credentials, $"sendOrder: {username}");
    await bus.PublishAsync(Topics.Orders, routingKey: order.Symbol, order);

    Console.WriteLine(
        $"Sent {side.ToString().ToUpperInvariant()} {quantity} {order.Symbol} @ {price.ToString("0.00", CultureInfo.InvariantCulture)} " +
        $"as {username} (order {order.OrderId:N}).");
    return 0;
}
catch (MessageBusException ex)
{
    Console.Error.WriteLine(ex.Message);
    return 2;
}

static bool TryParseArguments(
    string[] args, out string username, out string endpoint, out OrderSide side, out int quantity,
    out decimal price, out string symbol, [System.Diagnostics.CodeAnalysis.NotNullWhen(false)] out string? error)
{
    username = endpoint = string.Empty;
    side = OrderSide.Buy;
    quantity = 0;
    price = 0m;
    symbol = TradingNames.DefaultSymbol;
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
            symbol = args[++i];
        }
        else if (args[i].StartsWith('-') && !IsNegativeNumber(args[i]))
        {
            error = $"Unknown option '{args[i]}'.";
            return false;
        }
        else
        {
            positional.Add(args[i]);
        }
    }

    if (positional.Count != 5)
    {
        error = "Expected a username, endpoint, side, quantity and price.";
        return false;
    }

    username = positional[0];
    endpoint = positional[1];

    if (!Enum.TryParse(positional[2], ignoreCase: true, out side) || !Enum.IsDefined(side))
    {
        error = $"'{positional[2]}' is not a side. Use BUY or SELL.";
        return false;
    }

    if (!int.TryParse(positional[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out quantity) || quantity <= 0)
    {
        error = $"'{positional[3]}' is not a quantity. Use a whole number greater than zero.";
        return false;
    }

    if (!decimal.TryParse(positional[4], NumberStyles.Number, CultureInfo.InvariantCulture, out price) || price <= 0m)
    {
        error = $"'{positional[4]}' is not a price. Use a number greater than zero, such as 10.50.";
        return false;
    }

    string? nameError = TradingNames.ValidateUsername(username) ?? TradingNames.ValidateSymbol(symbol);
    if (nameError is not null)
    {
        error = nameError;
        return false;
    }

    symbol = TradingNames.NormaliseSymbol(symbol);
    return true;
}

// A negative number is a value someone typed, not an option, so it gets a price or quantity error.
static bool IsNegativeNumber(string text) =>
    text.Length > 1 && (char.IsAsciiDigit(text[1]) || text[1] == '.');

static void PrintUsage()
{
    Console.WriteLine($"""

        Usage: sendOrder <username> <endpoint> <BUY|SELL> <quantity> <price> [--symbol XYZ]

          <username>    Trader's name
          <endpoint>    RabbitMQ endpoint: 5672, host:5672 or amqp://host:5672
          <BUY|SELL>    Side of the order
          <quantity>    Whole number of shares (this assessment uses {TradingNames.FixedQuantity})
          <price>       Price per share, e.g. 10.50 (use a dot, not a comma)
          --symbol, -s  Stock symbol (default: {TradingNames.DefaultSymbol})

        RabbitMQ login: set PBT205_RABBITMQ_USER and PBT205_RABBITMQ_PASSWORD,
        or the app will ask for them.

        Example: sendOrder Andrew 100.74.122.113:5672 BUY {TradingNames.FixedQuantity} 10.50
        """);
}
