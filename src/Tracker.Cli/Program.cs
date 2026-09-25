// Task 3: tracks positions and answers contact queries.
//
// Usage: tracker <endpoint> [--board-size 10]
//
// TODO: not written yet. Follow the pattern in src/Chat.Cli/Program.cs:
//   1. Parse and validate the arguments.
//   2. BrokerEndpoint.TryParse, then BrokerCredentials.FromEnvironmentOrPrompt.
//   3. MessageBus.ConnectAsync, then publish or subscribe.

if (args.Any(a => a is "-h" or "--help" or "/?"))
{
    PrintUsage();
    return 0;
}


return 1;

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
