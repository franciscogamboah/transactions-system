namespace Transactions.Application.Contracts;

public sealed record ValidatedEvent(Guid TransactionExternalId, string Status, string? Reason, DateTimeOffset ValidatedAt);
