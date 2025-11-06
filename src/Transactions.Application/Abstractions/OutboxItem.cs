namespace Transactions.Application.Abstractions;

public sealed record OutboxItem(Guid Id, Guid AggregateId, string EventType, string Payload, DateTimeOffset CreatedAt);
