using Pbt205.Chat;

namespace Pbt205.Chat.Cli;

/// <summary>Writes to the console from several threads without lines getting mixed up.</summary>
internal static class ConsoleOutput
{
    private static readonly Lock Gate = new();

    public static void Chat(ChatMessage message)
    {
        string time = message.SentAt.ToLocalTime().ToString("HH:mm:ss");
        switch (message.Kind)
        {
            case ChatMessageKind.Message:
                Write($"[{time}] {message.Username}: {message.Text}", ConsoleColor.Cyan, message.Username.Length + 12);
                break;
            case ChatMessageKind.Joined:
                Info($"[{time}] {message.Username} joined #{message.Room}");
                break;
            case ChatMessageKind.Left:
                Info($"[{time}] {message.Username} left #{message.Room}");
                break;
        }
    }

    public static void Info(string text) => Write($"* {text}", ConsoleColor.DarkGray, text.Length + 2);

    public static void Error(string text)
    {
        lock (Gate)
        {
            ConsoleColor previous = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Error.WriteLine(text);
            Console.ForegroundColor = previous;
        }
    }

    /// <param name="colouredLength">How many leading characters to colour (the name part).</param>
    private static void Write(string line, ConsoleColor colour, int colouredLength)
    {
        colouredLength = Math.Min(colouredLength, line.Length);
        lock (Gate)
        {
            ConsoleColor previous = Console.ForegroundColor;
            Console.ForegroundColor = colour;
            Console.Write(line[..colouredLength]);
            Console.ForegroundColor = previous;
            Console.WriteLine(line[colouredLength..]);
        }
    }
}
