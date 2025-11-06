namespace Transactions.Api.Dtos;

public sealed record CreateTransactionResponse(Guid TransactionExternalId, string Status, DateTimeOffset CreatedAt);
