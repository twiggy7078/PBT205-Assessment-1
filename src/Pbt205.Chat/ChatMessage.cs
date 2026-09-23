namespace Pbt205.Chat;

public enum ChatMessageKind
{
    Message,
    Joined,
    Left,
}

/// <summary>
/// The JSON contract for everything sent to the "room" topic. Example:
/// { "kind": "Message", "senderId": "...", "username": "Jane", "room": "general",
///   "text": "hi", "sentAt": "2026-09-16T04:10:00+00:00" }
/// </summary>
/// <param name="SenderId">Unique per running app, so an app can ignore its own messages even if two people pick the same name.</param>
public sealed record ChatMessage(
    ChatMessageKind Kind,
    Guid SenderId,
    string Username,
    string Room,
    string Text,
    DateTimeOffset SentAt);
