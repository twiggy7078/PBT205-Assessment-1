// PBT205 Assessment 1, Task 1: command-line chat.
//
// Usage: chat <username> <endpoint> [--room <name>]
//   <username>  Name shown to other participants.
//   <endpoint>  RabbitMQ endpoint: a TCP port (5672), host:port, or amqp://host:port.
//   --room      Room to join (default "general").
//
// The RabbitMQ login comes from PBT205_RABBITMQ_USER and PBT205_RABBITMQ_PASSWORD, or is asked for at start-up.

using System.Text;
using Pbt205.Chat;
using Pbt205.Chat.Cli;
using Pbt205.Messaging;

Console.OutputEncoding = Encoding.UTF8;
Console.InputEncoding = Encoding.UTF8;

if (args.Any(a => a is "-h" or "--help" or "/?"))
{
    PrintUsage();
    return 0;
}

if (!TryParseArguments(args, out string username, out string endpointText, out string room, out string? argumentError))
{
    ConsoleOutput.Error(argumentError);
    PrintUsage();
    return 1;
}

string? nameError = ChatNames.ValidateUsername(username) ?? ChatNames.ValidateRoom(room);
if (nameError is not null)
{
    ConsoleOutput.Error(nameError);
    return 1;
}

if (!BrokerEndpoint.TryParse(endpointText, out BrokerEndpoint? endpoint, out string endpointError))
{
    ConsoleOutput.Error(endpointError);
    return 1;
}

BrokerCredentials? credentials = BrokerCredentials.FromEnvironmentOrPrompt();
if (credentials is null)
{
    ConsoleOutput.Error(
        $"No RabbitMQ login. Set {BrokerCredentials.UserVariable} and {BrokerCredentials.PasswordVariable}, " +
        "or run the app in a console so it can ask.");
    return 1;
}

using var shutdown = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    // Leave the room cleanly instead of killing the process.
    e.Cancel = true;
    shutdown.Cancel();
};

try
{
    await using MessageBus bus = await MessageBus.ConnectAsync(endpoint, credentials, $"chat: {username}", shutdown.Token);
    bus.StatusChanged += ConsoleOutput.Info;

    await using ChatRoomClient chat = await ChatRoomClient.JoinAsync(bus, username, room, shutdown.Token);
    chat.MessageReceived += ConsoleOutput.Chat;
    chat.ErrorOccurred += ex => ConsoleOutput.Error(ex.Message);

    ConsoleOutput.Info($"Connected to {endpoint} as {chat.Username} in #{chat.Room}.");
    ConsoleOutput.Info("Type a message and press Enter to send. Type /quit or press Ctrl+C to leave.");

    while (!shutdown.IsCancellationRequested)
    {
        string? line;
        try
        {
            // Console reads can't be cancelled directly, so wait for either a line or Ctrl+C.
            line = await Task.Run(Console.In.ReadLine).WaitAsync(shutdown.Token);
        }
        catch (OperationCanceledException)
        {
            break;
        }

        if (line is null || line.Trim().Equals("/quit", StringComparison.OrdinalIgnoreCase))
        {
            break; // End of input or the user asked to leave.
        }

        try
        {
            await chat.SendAsync(line, shutdown.Token);
        }
        catch (Exception ex) when (ex is ArgumentException or MessageBusException)
        {
            ConsoleOutput.Error(ex.Message);
        }
    }

    ConsoleOutput.Info($"Leaving #{chat.Room}.");
    return 0;
}
catch (MessageBusException ex)
{
    ConsoleOutput.Error(ex.Message);
    return 2;
}
catch (OperationCanceledException)
{
    return 0;
}

static bool TryParseArguments(
    string[] args, out string username, out string endpoint, out string room, [System.Diagnostics.CodeAnalysis.NotNullWhen(false)] out string? error)
{
    username = endpoint = string.Empty;
    room = ChatNames.DefaultRoom;
    error = null;
    var positional = new List<string>();

    for (int i = 0; i < args.Length; i++)
    {
        if (args[i] is "--room" or "-r")
        {
            if (i + 1 >= args.Length)
            {
                error = "--room needs a room name after it.";
                return false;
            }
            room = args[++i];
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

    if (positional.Count != 2)
    {
        error = "Expected a username and a middleware endpoint.";
        return false;
    }

    (username, endpoint) = (positional[0], positional[1]);
    return true;
}

static void PrintUsage()
{
    Console.WriteLine("""

        Usage: chat <username> <endpoint> [--room <name>]

          <username>   Name shown to other participants
          <endpoint>   RabbitMQ endpoint: 5672, host:5672 or amqp://host:5672
          --room, -r   Room to join (default: general)

        RabbitMQ login: set PBT205_RABBITMQ_USER and PBT205_RABBITMQ_PASSWORD,
        or the app will ask for them.

        Example: chat Andrew 100.74.122.113:5672 --room general
        """);
}
