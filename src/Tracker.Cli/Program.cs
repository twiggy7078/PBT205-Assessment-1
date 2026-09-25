// Task 3: tracks positions and answers contact queries.
//
// Usage: tracker <endpoint> [--board-size 10]
//
// TODO: not written yet. Follow the pattern in src/Chat.Cli/Program.cs:
//   1. Parse and validate the arguments.
//   2. BrokerEndpoint.TryParse, then BrokerCredentials.FromEnvironmentOrPrompt.
//   3. MessageBus.ConnectAsync, then publish or subscribe.

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
if (!TryParseArguments(args, out string endpointText, out int boardSize, out string? argumentError))
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
var tracker = new ContactTracker(board);
var stateLock = new SemaphoreSlim(1, 1);

using var shutdown = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    shutdown.Cancel();
};

try
{
    await using MessageBus bus = await MessageBus.ConnectAsync(endpoint, credentials, "tracker", shutdown.Token);
    bus.StatusChanged += message => Console.WriteLine($"* {message}");

    // sSubscribe to position updates from all connections
    await bus.SubscribeAsync<PositionMessage>(
        Topics.Position,
        "#",
        async (posMsg, ctx) =>
        {
            await stateLock.WaitAsync(shutdown.Token).ConfigureAwait(false);
            try
            {
                var contacts = tracker.UpdatePosition(posMsg.Person, posMsg.X, posMsg.Y, posMsg.MovedAt);
                Console.WriteLine($"> POSITION: {posMsg.Person} at ({posMsg.X}, {posMsg.Y})");
                foreach (var c in contacts)
                {
                    Console.WriteLine($"  > CONTACT: {posMsg.Person} met {c.Person} at ({c.X}, {c.Y})");
                }
            }
            finally
            {
                stateLock.Release();
            }
        },
        ex => Console.Error.WriteLine($"POSITION ERROR: {ex.Message}"),
        shutdown.Token);

    // also subscribe to query requests
    await bus.SubscribeAsync<ContactQuery>(
        Topics.Query,
        "#",
        async (query, ctx) =>
        {
            IReadOnlyList<Contact> contacts;
            await stateLock.WaitAsync(shutdown.Token).ConfigureAwait(false);
            try
            {
                contacts = tracker.GetContacts(query.Person);
            }
            finally
            {
                stateLock.Release();
            }

            var response = new ContactQueryResponse(
                query.CorrelationId,
                query.Person,
                contacts,
                DateTimeOffset.UtcNow);

            await bus.PublishAsync(
                Topics.QueryResponse,
                query.Person,
                response,
                shutdown.Token).ConfigureAwait(false);

            Console.WriteLine($"* Query for '{query.Person}' answered with {contacts.Count} contact(s).");
        },
        ex => Console.Error.WriteLine($"QUERY ERROR: {ex.Message}"),
        shutdown.Token);

    Console.WriteLine($"* Tracker for board [{board.Size}x{board.Size}] active. Waiting for positions and queries...");
    Console.WriteLine("* Press Ctrl+C to stop.");

    await Task.Delay(Timeout.Infinite, shutdown.Token);
    return 0;
}
catch (OperationCanceledException)
{
    Console.WriteLine("* Tracker stopped.");
    return 0;
}
catch (MessageBusException ex)
{
    Console.Error.WriteLine(ex.Message);
    return 2;
}
static bool TryParseArguments(
    string[] args,
    out string endpointText,
    out int boardSize,
    [System.Diagnostics.CodeAnalysis.NotNullWhen(false)] out string? error)
{
    endpointText = string.Empty;
    boardSize = Board.DefaultSize;
    error = null;

    var positional = new List<string>();

    for (var i = 0; i < args.Length; i++)
    {
        if (args[i] is "--board-size" or "-b")
        {
            if (i + 1 < args.Length && int.TryParse(args[++i], out boardSize) && boardSize > 0) continue;
            error = $"Option '{args[i - 1]}' requires a positive integer board size.";
            return false;
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
        error = "Expected exactly one middleware endpoint argument.";
        return false;
    }

    endpointText = positional[0];
    return true;
}

static void PrintUsage()
{
    Console.WriteLine("""

                      Usage: tracker <endpoint> [--board-size <size>]

                        <endpoint>          RabbitMQ endpoint: 5672, host:5672 or amqp://host:5672
                        --board-size, -b    the starting size of the board (default: 10)

                      RabbitMQ login: set PBT205_RABBITMQ_USER and PBT205_RABBITMQ_PASSWORD,
                      or the app will ask for them.

                      Example: chat Andrew 100.74.122.113:5672 --room general
                      """);
}
