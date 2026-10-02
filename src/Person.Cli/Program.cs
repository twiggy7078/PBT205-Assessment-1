using System.Globalization;
using System.Text;
using Pbt205.Messaging;
using Pbt205.Tracing;

Console.OutputEncoding = Encoding.UTF8;
Console.InputEncoding = Encoding.UTF8;

if (args.Any(a => a is "-h" or "--help" or "/?"))
{
    PrintUsage();
    return 0;
}

// Parse and validate arguments
if (!TryParseArguments(
        args,
        out string endpointText,
        out string personId,
        out double movesPerSecond,
        out int boardSize,
        out string? argumentError))
{
    Console.Error.WriteLine(argumentError);
    PrintUsage();
    return 1;
}

// Validate the endpoint format using BrokerEndpoint.TryParse
if (!BrokerEndpoint.TryParse(endpointText, out BrokerEndpoint? endpoint, out string endpointError))
{
    Console.Error.WriteLine(endpointError);
    return 1;
}

// Retrieve credentials and connect
BrokerCredentials? credentials = BrokerCredentials.FromEnvironmentOrPrompt();
if (credentials is null)
{
    Console.Error.WriteLine(
        $"No RabbitMQ login. Set {BrokerCredentials.UserVariable} and {BrokerCredentials.PasswordVariable}, " +
        "or run the app in a console so it can ask.");
    return 1;
}

var board = new Board(boardSize);
var clientId = Guid.NewGuid();
using var shutdown = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    shutdown.Cancel();
};

try
{
    await using MessageBus bus = await MessageBus.ConnectAsync(
        endpoint,
        credentials,
        $"person: {personId}",
        shutdown.Token);

    bus.StatusChanged += message => Console.WriteLine($"* {message}");

    // Spawn at a random location on the board
    int x = Random.Shared.Next(0, board.Size);
    int y = Random.Shared.Next(0, board.Size);

    TimeSpan moveInterval = TimeSpan.FromSeconds(1.0 / movesPerSecond);
    using var timer = new PeriodicTimer(moveInterval);

    Console.WriteLine($"* Person '{personId}' active on {board.Size}x{board.Size} board at {movesPerSecond:0.##} moves/sec.");
    Console.WriteLine($"* Starting position: [{x}, {y}]. Press Ctrl+C to stop.");

    // Publish initial starting position
    await PublishPositionAsync(bus, clientId, personId, x, y, shutdown.Token).ConfigureAwait(false);

    // Main simulation loop
    while (!shutdown.IsCancellationRequested && await timer.WaitForNextTickAsync(shutdown.Token).ConfigureAwait(false))
    {
        // Random step in 8 directions (or stay)
        int dx = Random.Shared.Next(-1, 2);
        int dy = Random.Shared.Next(-1, 2);

        // Clamp inside board boundaries
        (x, y) = board.Clamp(x + dx, y + dy);

        await PublishPositionAsync(bus, clientId, personId, x, y, shutdown.Token).ConfigureAwait(false);
    }

    return 0;
}
catch (OperationCanceledException)
{
    Console.WriteLine($"\n* Person '{personId}' stopped.");
    return 0;
}
catch (MessageBusException ex)
{
    Console.Error.WriteLine($"COMMS ERROR: {ex.Message}");
    return 2;
}

static async Task PublishPositionAsync(
    MessageBus bus,
    Guid clientId,
    string personId,
    int x,
    int y,
    CancellationToken cancellationToken)
{
    var message = new PositionMessage(clientId, personId, x, y, DateTimeOffset.UtcNow);
    await bus.PublishAsync(Topics.Position, personId, message, cancellationToken).ConfigureAwait(false);
    Console.WriteLine($"> [{message.MovedAt:HH:mm:ss.fff}] {personId} at [{x}, {y}]");
}

static bool TryParseArguments(
    string[] args,
    out string endpoint,
    out string personId,
    out double movesPerSecond,
    out int boardSize,
    [System.Diagnostics.CodeAnalysis.NotNullWhen(false)] out string? error)
{
    endpoint = string.Empty;
    personId = string.Empty;
    movesPerSecond = 1.0;
    boardSize = Board.DefaultSize;
    error = null;

    var positional = new List<string>();

    for (var i = 0; i < args.Length; i++)
    {
        switch (args[i])
        {
            case "--moves-per-second" or "-m" when 
                i + 1 < args.Length &&
                double.TryParse(args[++i], NumberStyles.Float, CultureInfo.InvariantCulture, out movesPerSecond) &&
                !(movesPerSecond <= 0):
                    continue;
            case "--moves-per-second" or "-m":
                error = $"Option '{args[i - 1]}' requires a positive movement rate (e.g. 1 or 0.5).";
                return false;
            case "--board-size" or "-b":
            {
                // validation includes assigning the board size
                if (i + 1 >= args.Length || !int.TryParse(args[++i], out boardSize) || boardSize is <= 1 or > 1000)
                {
                    error = $"Option '{args[i - 1]}' requires a positive integer board size greater than 1 and less than or equal to 1000.";
                    return false;
                }

                break;
            }
            default:
            {
                if (args[i].StartsWith('-'))
                {
                    error = $"Unknown option '{args[i]}'.";
                    return false;
                }

                positional.Add(args[i]);

                break;
            }
        }
    }

    if (positional.Count != 2)
    {
        error = "Expected an endpoint and a person identifier.";
        return false;
    }

    endpoint = positional[0];
    personId = positional[1];

    if (!string.IsNullOrWhiteSpace(personId)) return true;
    error = "Person identifier cannot be empty.";
    return false;

}

static void PrintUsage()
{
    Console.WriteLine("""

        Usage: person <endpoint> <person-id> [--moves-per-second <n>] [--board-size <size>]

          <endpoint>                 RabbitMQ endpoint: 5672, host:5672 or amqp://host:5672
          <person-id>                Unique identifier for this person
          --moves-per-second, -m     Move rate in Hz (default: 1)
          --board-size, -b           Board dimension NxN (default: 10)

        RabbitMQ login: set PBT205_RABBITMQ_USER and PBT205_RABBITMQ_PASSWORD,
        or the app will ask for them.

        Example: person 5672 Alice --moves-per-second 2 --board-size 10
        """);
}