using System.Text;

namespace Pbt205.Messaging;

/// <summary>
/// RabbitMQ login. Read from environment variables so passwords never end up in source code,
/// or asked for at start-up when the variables are not set.
/// </summary>
public sealed record BrokerCredentials(string UserName, string Password)
{
    public const string UserVariable = "PBT205_RABBITMQ_USER";
    public const string PasswordVariable = "PBT205_RABBITMQ_PASSWORD";

    public override string ToString() => $"{UserName} (password hidden)";

    public static BrokerCredentials? FromEnvironment()
    {
        string? user = Environment.GetEnvironmentVariable(UserVariable);
        string? password = Environment.GetEnvironmentVariable(PasswordVariable);
        return string.IsNullOrWhiteSpace(user) || password is null
            ? null
            : new BrokerCredentials(user.Trim(), password);
    }

    /// <summary>
    /// Uses the environment variables if set; otherwise prompts on the console.
    /// Returns null if there is no way to get a login (for example, input is redirected).
    /// </summary>
    public static BrokerCredentials? FromEnvironmentOrPrompt()
    {
        BrokerCredentials? fromEnvironment = FromEnvironment();
        if (fromEnvironment is not null)
        {
            return fromEnvironment;
        }

        if (Console.IsInputRedirected)
        {
            return null;
        }

        Console.Write("RabbitMQ username: ");
        string user = Console.ReadLine()?.Trim() ?? string.Empty;
        if (user.Length == 0)
        {
            return null;
        }

        Console.Write("RabbitMQ password: ");
        string password = ReadHidden();
        return new BrokerCredentials(user, password);
    }

    private static string ReadHidden()
    {
        var buffer = new StringBuilder();
        while (true)
        {
            ConsoleKeyInfo key = Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Enter)
            {
                Console.WriteLine();
                return buffer.ToString();
            }

            if (key.Key == ConsoleKey.Backspace)
            {
                if (buffer.Length > 0)
                {
                    buffer.Length--;
                    Console.Write("\b \b");
                }
            }
            else if (!char.IsControl(key.KeyChar))
            {
                buffer.Append(key.KeyChar);
                Console.Write('*');
            }
        }
    }
}
