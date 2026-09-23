using System.Net.Sockets;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;

namespace Pbt205.Messaging;

/// <summary>Details about a received message that are not part of its JSON body.</summary>
public sealed record MessageContext(string Topic, string RoutingKey, DateTimeOffset ReceivedAt);

/// <summary>
/// A thin wrapper over one RabbitMQ connection that every prototype app uses.
///
/// Design:
/// * Each brief "topic" is a durable RabbitMQ topic exchange (see <see cref="Topics"/>).
/// * Publishing sends JSON to the exchange with a routing key (room name, stock symbol, ...).
/// * Subscribing creates a private, server-named queue for this app only and binds it to the exchange,
///   so every subscriber gets its own copy of each message (fan-out), rather than competing for them.
/// </summary>
public sealed class MessageBus : IAsyncDisposable
{
    private readonly IConnection _connection;
    private readonly IChannel _publishChannel;
    private readonly SemaphoreSlim _publishLock = new(1, 1);
    private readonly List<Subscription> _subscriptions = [];
    private readonly HashSet<string> _declaredOnPublishChannel = [];
    private bool _disposed;

    private MessageBus(IConnection connection, IChannel publishChannel, BrokerEndpoint endpoint)
    {
        _connection = connection;
        _publishChannel = publishChannel;
        Endpoint = endpoint;

        _connection.ConnectionShutdownAsync += (_, e) =>
        {
            if (!_disposed)
            {
                StatusChanged?.Invoke($"Connection lost ({e.ReplyText}). Trying to reconnect...");
            }
            return Task.CompletedTask;
        };
        _connection.RecoverySucceededAsync += (_, _) =>
        {
            StatusChanged?.Invoke("Reconnected.");
            return Task.CompletedTask;
        };
    }

    public BrokerEndpoint Endpoint { get; }

    public bool IsOpen => _connection.IsOpen;

    /// <summary>Raised when the connection drops or recovers. Safe to show to the user.</summary>
    public event Action<string>? StatusChanged;

    /// <summary>Opens a connection. <paramref name="clientName"/> is shown on the management page's Connections tab.</summary>
    public static async Task<MessageBus> ConnectAsync(
        BrokerEndpoint endpoint,
        BrokerCredentials credentials,
        string clientName,
        CancellationToken cancellationToken = default)
    {
        var factory = new ConnectionFactory
        {
            HostName = endpoint.Host,
            Port = endpoint.Port,
            UserName = credentials.UserName,
            Password = credentials.Password,
            ClientProvidedName = clientName,
            AutomaticRecoveryEnabled = true,
            TopologyRecoveryEnabled = true,
            NetworkRecoveryInterval = TimeSpan.FromSeconds(5),
            RequestedConnectionTimeout = TimeSpan.FromSeconds(10),
        };

        IConnection connection;
        try
        {
            connection = await factory.CreateConnectionAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (BrokerUnreachableException ex)
        {
            throw new MessageBusException(DescribeConnectFailure(ex, endpoint, credentials), ex);
        }

        try
        {
            IChannel channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
            return new MessageBus(connection, channel, endpoint);
        }
        catch
        {
            await connection.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>Publishes <paramref name="message"/> as JSON to a topic.</summary>
    public async Task PublishAsync<T>(string topic, string routingKey, T message, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var properties = new BasicProperties
        {
            ContentType = JsonMessageSerializer.ContentType,
            Type = typeof(T).Name,
            MessageId = Guid.NewGuid().ToString("N"),
            Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds()),
        };
        byte[] body = JsonMessageSerializer.Serialize(message);

        // A channel must not be used by two publishers at the same time.
        await _publishLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_declaredOnPublishChannel.Add(topic))
            {
                await DeclareTopicAsync(_publishChannel, topic, cancellationToken).ConfigureAwait(false);
            }

            await _publishChannel.BasicPublishAsync(topic, routingKey, mandatory: false, properties, body, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (AlreadyClosedException ex)
        {
            throw new MessageBusException("Can't send right now: the connection to RabbitMQ is closed.", ex);
        }
        finally
        {
            _publishLock.Release();
        }
    }

    /// <summary>
    /// Starts receiving messages from <paramref name="topic"/> whose routing key matches <paramref name="bindingKey"/>
    /// ("*" matches one word, "#" matches everything). Dispose the result to stop.
    /// Messages that cannot be read as <typeparamref name="T"/>, and exceptions thrown by the handler,
    /// are passed to <paramref name="onError"/> instead of stopping the subscription.
    /// </summary>
    public async Task<IAsyncDisposable> SubscribeAsync<T>(
        string topic,
        string bindingKey,
        Func<T, MessageContext, Task> handler,
        Action<Exception>? onError = null,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        IChannel channel = await _connection.CreateChannelAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        try
        {
            await DeclareTopicAsync(channel, topic, cancellationToken).ConfigureAwait(false);

            // Empty name = RabbitMQ picks a unique name. Exclusive + auto-delete = removed when this app disconnects.
            QueueDeclareOk queue = await channel.QueueDeclareAsync(
                queue: string.Empty, durable: false, exclusive: true, autoDelete: true,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            await channel.QueueBindAsync(queue.QueueName, topic, bindingKey, cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            var consumer = new AsyncEventingBasicConsumer(channel);
            consumer.ReceivedAsync += async (_, delivery) =>
            {
                T message;
                try
                {
                    // The body buffer is only valid inside this callback, so read it straight away.
                    message = JsonMessageSerializer.Deserialize<T>(delivery.Body.Span);
                }
                catch (Exception ex)
                {
                    onError?.Invoke(new MessageBusException(
                        $"Ignored an unreadable message on '{topic}' ({delivery.RoutingKey}): {ex.Message}", ex));
                    return;
                }

                try
                {
                    await handler(message, new MessageContext(topic, delivery.RoutingKey, DateTimeOffset.Now))
                        .ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    onError?.Invoke(ex);
                }
            };

            string consumerTag = await channel.BasicConsumeAsync(
                queue.QueueName, autoAck: true, consumer, cancellationToken).ConfigureAwait(false);

            var subscription = new Subscription(this, channel, consumerTag);
            lock (_subscriptions)
            {
                _subscriptions.Add(subscription);
            }
            return subscription;
        }
        catch
        {
            await channel.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;

        Subscription[] open;
        lock (_subscriptions)
        {
            open = [.. _subscriptions];
        }
        foreach (Subscription subscription in open)
        {
            await subscription.DisposeAsync().ConfigureAwait(false);
        }

        await SafeCloseAsync(_publishChannel).ConfigureAwait(false);
        try
        {
            await _connection.CloseAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
        }
        catch (Exception)
        {
            // Already closed or unreachable; nothing more to do.
        }
        await _connection.DisposeAsync().ConfigureAwait(false);
        _publishLock.Dispose();
    }

    private static Task DeclareTopicAsync(IChannel channel, string topic, CancellationToken cancellationToken) =>
        // Durable so the exchange survives a broker restart. Declaring is idempotent.
        channel.ExchangeDeclareAsync(topic, ExchangeType.Topic, durable: true, autoDelete: false,
            cancellationToken: cancellationToken);

    private static async Task SafeCloseAsync(IChannel channel)
    {
        try
        {
            if (channel.IsOpen)
            {
                await channel.CloseAsync().ConfigureAwait(false);
            }
        }
        catch (Exception)
        {
            // Closing is best effort.
        }
        await channel.DisposeAsync().ConfigureAwait(false);
    }

    private static string DescribeConnectFailure(Exception ex, BrokerEndpoint endpoint, BrokerCredentials credentials)
    {
        for (Exception? e = ex; e is not null; e = e.InnerException)
        {
            switch (e)
            {
                case AuthenticationFailureException or PossibleAuthenticationFailureException:
                    return $"RabbitMQ at {endpoint} refused the login for '{credentials.UserName}'. " +
                           "Check the username and password (both are case-sensitive).";
                case OperationInterruptedException { ShutdownReason.ReplyCode: 530 }:
                    return $"'{credentials.UserName}' has no access to the virtual host on {endpoint}. " +
                           "Set permissions for this user on '/' in the management page.";
                case SocketException or ConnectFailureException:
                    return $"Could not reach RabbitMQ at {endpoint}. Check the address and port, " +
                           "that the container is running, and that Tailscale is connected.";
            }
        }
        return $"Could not connect to RabbitMQ at {endpoint}: {ex.Message}";
    }

    private sealed class Subscription(MessageBus owner, IChannel channel, string consumerTag) : IAsyncDisposable
    {
        private int _disposed;

        public async ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 1)
            {
                return;
            }

            lock (owner._subscriptions)
            {
                owner._subscriptions.Remove(this);
            }

            try
            {
                if (channel.IsOpen)
                {
                    await channel.BasicCancelAsync(consumerTag).ConfigureAwait(false);
                }
            }
            catch (Exception)
            {
                // The channel may already be gone.
            }
            await SafeCloseAsync(channel).ConfigureAwait(false);
        }
    }
}
