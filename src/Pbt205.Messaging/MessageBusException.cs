namespace Pbt205.Messaging;

/// <summary>A connection or messaging problem, with a message that is safe to show a user.</summary>
public sealed class MessageBusException(string message, Exception? inner = null) : Exception(message, inner);
