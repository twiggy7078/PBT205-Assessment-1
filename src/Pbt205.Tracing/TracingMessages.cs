namespace Pbt205.Tracing;

/// <summary>
/// A person's position, published to the "position" topic. Routing key is the person's name.
/// { "person": "andrew", "x": 3, "y": 7, "movedAt": "2026-09-23T04:00:00+00:00" }
/// </summary>
public sealed record PositionMessage(string Person, int X, int Y, DateTimeOffset MovedAt);

/// <summary>
/// A request sent to the "query" topic asking who a person has been in contact with.
/// The tracker replies on "query-response" using the same CorrelationId,
/// so each query app only reads its own answer.
/// </summary>
public sealed record ContactQuery(Guid CorrelationId, string Person, DateTimeOffset AskedAt);

/// <summary>A single contact: who, where and when.</summary>
public sealed record Contact(string Person, int X, int Y, DateTimeOffset MetAt);

/// <summary>The tracker's reply on "query-response", contacts in reverse chronological order.</summary>
public sealed record ContactQueryResponse(
    Guid CorrelationId,
    string Person,
    IReadOnlyList<Contact> Contacts,
    DateTimeOffset AnsweredAt);
