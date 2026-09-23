// Task 2: submits one order and exits.
//
// Usage: sendOrder <username> <endpoint> <BUY|SELL> <quantity> <price> [--symbol XYZ]
//
// TODO: not written yet. Follow the pattern in src/Chat.Cli/Program.cs:
//   1. Parse and validate the arguments.
//   2. BrokerEndpoint.TryParse, then BrokerCredentials.FromEnvironmentOrPrompt.
//   3. MessageBus.ConnectAsync, then publish or subscribe.

Console.WriteLine("Usage: sendOrder <username> <endpoint> <BUY|SELL> <quantity> <price> [--symbol XYZ]");
Console.Error.WriteLine("Not written yet (see the TODO at the top of Program.cs).");
return 1;
