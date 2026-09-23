using System.Diagnostics.CodeAnalysis;

namespace Pbt205.Messaging;

/// <summary>
/// The middleware endpoint given as a start-up argument.
/// Accepts "5672" (localhost), "host:5672", "host" (default port) or "amqp://host:5672".
/// </summary>
public sealed record BrokerEndpoint(string Host, int Port)
{
    public const int DefaultPort = 5672;

    public static bool TryParse(string? text, [NotNullWhen(true)] out BrokerEndpoint? endpoint, out string error)
    {
        endpoint = null;
        error = string.Empty;
        text = text?.Trim();

        if (string.IsNullOrEmpty(text))
        {
            error = "The middleware endpoint is empty.";
            return false;
        }

        if (text.StartsWith("amqp://", StringComparison.OrdinalIgnoreCase))
        {
            text = text["amqp://".Length..].TrimEnd('/');
        }

        string host;
        string? portText;

        // A bare number is treated as a TCP port on this machine, as the brief describes.
        if (text.All(char.IsDigit))
        {
            host = "localhost";
            portText = text;
        }
        else
        {
            int colon = text.LastIndexOf(':');
            host = colon >= 0 ? text[..colon] : text;
            portText = colon >= 0 ? text[(colon + 1)..] : null;
        }

        if (string.IsNullOrWhiteSpace(host) || host.Any(char.IsWhiteSpace))
        {
            error = $"'{text}' does not contain a valid host name.";
            return false;
        }

        int port = DefaultPort;
        if (portText is not null && (!int.TryParse(portText, out port) || port is < 1 or > 65535))
        {
            error = $"'{portText}' is not a valid TCP port (1-65535).";
            return false;
        }

        endpoint = new BrokerEndpoint(host, port);
        return true;
    }

    public override string ToString() => $"{Host}:{Port}";
}
