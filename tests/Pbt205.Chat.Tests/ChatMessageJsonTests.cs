using System.Text;
using System.Text.Json;
using Pbt205.Messaging;

namespace Pbt205.Chat.Tests;

public class ChatMessageJsonTests
{
    private static readonly ChatMessage Sample = new(
        ChatMessageKind.Message,
        Guid.Parse("6f9619ff-8b86-d011-b42d-00c04fc964ff"),
        "Jane",
        "general",
        "hi",
        new DateTimeOffset(2026, 9, 16, 4, 10, 0, TimeSpan.Zero));

    [Fact]
    public void JsonUsesCamelCaseNamesAndKindAsText()
    {
        string json = Encoding.UTF8.GetString(JsonMessageSerializer.Serialize(Sample));

        Assert.Contains("\"kind\":\"Message\"", json);
        Assert.Contains("\"senderId\":", json);
        Assert.Contains("\"username\":\"Jane\"", json);
        Assert.Contains("\"room\":\"general\"", json);
        Assert.Contains("\"text\":\"hi\"", json);
        Assert.Contains("\"sentAt\":", json);
    }

    [Fact]
    public void MessageSurvivesRoundTrip()
    {
        byte[] body = JsonMessageSerializer.Serialize(Sample);
        Assert.Equal(Sample, JsonMessageSerializer.Deserialize<ChatMessage>(body));
    }

    [Fact]
    public void NullBodyIsRejected() =>
        Assert.Throws<JsonException>(() => JsonMessageSerializer.Deserialize<ChatMessage>("null"u8));

    [Fact]
    public void MalformedJsonIsRejected() =>
        Assert.Throws<JsonException>(() => JsonMessageSerializer.Deserialize<ChatMessage>("{ not json"u8));
}
