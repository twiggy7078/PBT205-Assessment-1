using Pbt205.Messaging;

namespace Pbt205.Chat;

/// <summary>
/// One user's membership of one chat room. Create with <see cref="JoinAsync"/>; dispose to leave.
/// The <see cref="MessageBus"/> is not owned, so a GUI can leave one room and join another on the same connection.
/// </summary>
public sealed class ChatRoomClient : IAsyncDisposable
{
    private readonly MessageBus _bus;
    private IAsyncDisposable? _subscription;
    private int _disposed;

    private ChatRoomClient(MessageBus bus, string username, string room)
    {
        _bus = bus;
        Username = username;
        Room = room;
    }

    public string Username { get; }

    public string Room { get; }

    /// <summary>Identifies this running app in every message it sends.</summary>
    public Guid SenderId { get; } = Guid.NewGuid();

    /// <summary>Raised for messages and join/leave notices from other participants (never your own).</summary>
    public event Action<ChatMessage>? MessageReceived;

    /// <summary>Raised when an incoming message could not be handled.</summary>
    public event Action<Exception>? ErrorOccurred;

    public static async Task<ChatRoomClient> JoinAsync(
        MessageBus bus, string username, string room, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(bus);
        string? error = ChatNames.ValidateUsername(username) ?? ChatNames.ValidateRoom(room);
        if (error is not null)
        {
            throw new ArgumentException(error);
        }

        var client = new ChatRoomClient(bus, username.Trim(), ChatNames.NormaliseRoom(room));

        // Subscribe before announcing, so replies to the join notice are not missed.
        client._subscription = await bus.SubscribeAsync<ChatMessage>(
            Topics.Room,
            bindingKey: client.Room,
            client.OnMessageAsync,
            ex => client.ErrorOccurred?.Invoke(ex),
            cancellationToken).ConfigureAwait(false);

        await client.PublishAsync(ChatMessageKind.Joined, string.Empty, cancellationToken).ConfigureAwait(false);
        return client;
    }

    /// <summary>Sends a chat message to everyone else in the room. Blank messages are ignored.</summary>
    public Task SendAsync(string text, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed == 1, this);
        ArgumentNullException.ThrowIfNull(text);

        text = text.Trim();
        if (text.Length == 0)
        {
            return Task.CompletedTask;
        }
        if (text.Length > ChatNames.MaxMessageLength)
        {
            throw new ArgumentException($"Messages must be {ChatNames.MaxMessageLength} characters or fewer.", nameof(text));
        }

        return PublishAsync(ChatMessageKind.Message, text, cancellationToken);
    }

    /// <summary>Announces that the user left, then stops receiving messages.</summary>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
        {
            return;
        }

        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            await PublishAsync(ChatMessageKind.Left, string.Empty, timeout.Token).ConfigureAwait(false);
        }
        catch (Exception)
        {
            // Leaving still works if the notice can't be sent (for example, the connection is down).
        }

        if (_subscription is not null)
        {
            await _subscription.DisposeAsync().ConfigureAwait(false);
        }
    }

    private Task PublishAsync(ChatMessageKind kind, string text, CancellationToken cancellationToken) =>
        _bus.PublishAsync(
            Topics.Room,
            routingKey: Room,
            new ChatMessage(kind, SenderId, Username, Room, text, DateTimeOffset.UtcNow),
            cancellationToken);

    private Task OnMessageAsync(ChatMessage message, MessageContext context)
    {
        // JSON from another app might be missing fields even though the types say they can't be null.
        if (string.IsNullOrWhiteSpace(message.Username) || message.Text is null || message.Room != Room)
        {
            ErrorOccurred?.Invoke(new InvalidDataException(
                $"Ignored an incomplete chat message on '{context.RoutingKey}'."));
            return Task.CompletedTask;
        }

        if (message.SenderId != SenderId)
        {
            MessageReceived?.Invoke(message);
        }
        return Task.CompletedTask;
    }
}
