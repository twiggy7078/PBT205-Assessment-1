namespace Pbt205.Messaging;

/// <summary>
/// Names of the "topics" from the assessment brief. Each one is a RabbitMQ topic exchange.
/// Keeping them in one place means every app agrees on the spelling.
/// </summary>
public static class Topics
{
    // Task 1: chat. The routing key is the room name, e.g. "general".
    public const string Room = "room";

    // Task 2: trading. The routing key is the stock symbol, e.g. "XYZ".
    public const string Orders = "orders";
    public const string Trades = "trades";

    // Beyond the brief: the exchange publishes the state of the order book so a GUI can show
    // orders that are still waiting. Routing key is the stock symbol.
    public const string Book = "book";

    // Task 3: contact tracing.
    public const string Position = "position";
    public const string Query = "query";
    public const string QueryResponse = "query-response";
}
