namespace Pbt205.Chat;

/// <summary>Validation rules for usernames, room names and message text.</summary>
public static class ChatNames
{
    public const string DefaultRoom = "general";
    public const int MaxUsernameLength = 32;
    public const int MaxRoomLength = 32;
    public const int MaxMessageLength = 2000;

    /// <returns>An error message, or null if the username is fine.</returns>
    public static string? ValidateUsername(string? username)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            return "Username can't be empty.";
        }
        if (username.Trim().Length > MaxUsernameLength)
        {
            return $"Username must be {MaxUsernameLength} characters or fewer.";
        }
        return username.Any(char.IsControl) ? "Username contains invalid characters." : null;
    }

    /// <summary>
    /// Room names become RabbitMQ routing keys, where "." "*" and "#" have special meaning,
    /// so only letters, digits, "-" and "_" are allowed.
    /// </summary>
    /// <returns>An error message, or null if the room name is fine.</returns>
    public static string? ValidateRoom(string? room)
    {
        if (string.IsNullOrWhiteSpace(room))
        {
            return "Room name can't be empty.";
        }
        if (room.Length > MaxRoomLength)
        {
            return $"Room name must be {MaxRoomLength} characters or fewer.";
        }
        return room.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_')
            ? null
            : "Room names can only use letters, digits, '-' and '_'.";
    }

    /// <summary>Room names are case-insensitive: "General" and "general" are the same room.</summary>
    public static string NormaliseRoom(string room) => room.Trim().ToLowerInvariant();
}
