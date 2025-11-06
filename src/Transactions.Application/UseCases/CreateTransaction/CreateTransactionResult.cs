namespace Transactions.Application.UserCases.CreateTransaction;

public sealed record CreateTransactionResult(Guid TransactionExternalId, string Status, DateTimeOffset CreatedAt);