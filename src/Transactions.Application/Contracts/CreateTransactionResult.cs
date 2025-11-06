namespace Transactions.Application.Contracts;

public sealed record CreateTransactionResult(Guid TransactionExternalId, string Status, DateTimeOffset CreatedAt);
