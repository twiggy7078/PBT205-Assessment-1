using System.Text.Json;
using System.Text.Json.Serialization;

namespace Pbt205.Messaging;

/// <summary>
/// All messages travel as UTF-8 JSON with camelCase names and enums as strings,
/// so they are readable in the RabbitMQ management page.
/// </summary>
public static class JsonMessageSerializer
{
    public const string ContentType = "application/json";

    public static JsonSerializerOptions Options { get; } = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public static byte[] Serialize<T>(T message) => JsonSerializer.SerializeToUtf8Bytes(message, Options);

    public static T Deserialize<T>(ReadOnlySpan<byte> body) =>
        JsonSerializer.Deserialize<T>(body, Options)
        ?? throw new JsonException($"The message body was empty or null, expected {typeof(T).Name}.");
}
