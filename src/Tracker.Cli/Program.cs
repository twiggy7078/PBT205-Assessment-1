// Task 3: tracks positions and answers contact queries.
//
// Usage: tracker <endpoint> [--board-size 10]
//
// TODO: not written yet. Follow the pattern in src/Chat.Cli/Program.cs:
//   1. Parse and validate the arguments.
//   2. BrokerEndpoint.TryParse, then BrokerCredentials.FromEnvironmentOrPrompt.
//   3. MessageBus.ConnectAsync, then publish or subscribe.

using System;
using Pbt205.Tracing;

if (args.Any(a => a is "-h" or "--help" or "/?"))
{
    PrintUsage();
    return 0;
}

TryParseArguments(args, out string endpoint, out int boardSize, out string? argumentError);



return 1;

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

    for (int i = 0; i < args.Length; i++)
    {
        if (args[i] is "--board-size" or "-b")
        {
            if (i + 1 >= args.Length || !int.TryParse(args[++i], out boardSize) || boardSize <= 0)
            {
                error = $"Option '{args[i - 1]}' requires a positive integer board size.";
                return false;
            }
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
