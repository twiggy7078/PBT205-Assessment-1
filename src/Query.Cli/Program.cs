// Task 3: asks who a person has been in contact with.
//
// Usage: query <endpoint> <person-id>
//
// TODO: not written yet. Follow the pattern in src/Chat.Cli/Program.cs:
//   1. Parse and validate the arguments.
//   2. BrokerEndpoint.TryParse, then BrokerCredentials.FromEnvironmentOrPrompt.
//   3. MessageBus.ConnectAsync, then publish or subscribe.

/*
Console.WriteLine("Usage: query <endpoint> <person-id>");
Console.Error.WriteLine("Not written yet (see the TODO at the top of Program.cs).");
return 1;
*/ 

using Pbt205.Messaging;
using Pbt205.Tracing;

//Usage: query <endpoint> <person-id>

if (args.Any(a => a is "-h" or "--help" or "/?"))
{
    PrintUsage();
    return 0;
}

if (args.Length != 2)
{
    Console.Error.WriteLine("expected endpoint and a person identifier");
    PrintUsage();
    return 1;
}

string endpointText = args[0];
string personId = args[1].Trim();

if (string.IsNullOrWhiteSpace(personId))
{
    Console.Error.WriteLine("Person identifier cannot be empty");
    return 1;
}

if (!BrokerEndpoint.TryParse(
    endpointText,
    out BrokerEndpoint? endpoint,
    out string endpointError))
{
    Console.Error.WriteLine(endpointError);
    return 1;
}

BrokerCredentials? credentials = 
    BrokerCredentials.FromEnvironmentOrPrompt();

if (credentials is null)
{
    Console.Error.WriteLine("No RabbiqMQ login provided");
    return 1;
}

try
{
    await using MessageBus bus = await MessageBus.ConnectAsync(
        endpoint, 
        credentials,
        $"query: {personId}");

    Console.WriteLine($"Connected to RabbitMQ at {endpoint}.");
    
    Guid correlationId = Guid.NewGuid();

    var answer = new TaskCompletionSource<ContactQueryResponse>(
        TaskCreationOptions.RunContinuationsAsynchronously);

    // Subscribe first so we cannot miss a qucik response
    await bus.SubscribeAsync<ContactQueryResponse>(
        Topics.QueryResponse,
        "#",
        (response, _) =>
        {
            if (response.CorrelationId == correlationId)
            {
                answer.TrySetResult(response);
            }
            
            return Task.CompletedTask;
        },
        ex => answer.TrySetException(ex));

var query = new ContactQuery(
    correlationId,
    personId,
    DateTimeOffset.UtcNow);

await bus.PublishAsync(Topics.Query, personId, query);

Console.WriteLine($"Waiting for contacts for '{personId}'...");

ContactQueryResponse result;

try
{
    result = await answer.Task.WaitAsync(TimeSpan.FromSeconds(15));
}
catch(TimeoutException)
{
    Console.Error.WriteLine(
        "No response within 15 seconds. Check that the tracker is running.");
        return 3;
}

if (result.Contacts.Count == 0)
{
    Console.WriteLine($"No recorded contacts for '{result.Person}'.");
}
else
{
    Console.WriteLine($"Contacts for '{result.Person}', newest first");

    foreach (Contact contact in result.Contacts.OrderByDescending(c => c.MetAt))
    {
        Console.WriteLine(
            $"{contact.MetAt.ToLocalTime():yyyy-MM-dd HH:mm:ss} | " + 
            $"{contact.Person} | ({contact.X}, {contact.Y})");
    }
}
return 0;

}
catch (MessageBusException ex)
{
    Console.Error.WriteLine(ex.Message);
    return 2;
}

static void PrintUsage()
{
    Console.WriteLine(
        "Usage: query <endpoint> <person-id>");
}