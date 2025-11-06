namespace Transactions.Api.Dtos;

public sealed record GetTransactionResponse(Guid TransactionExternalId, string Status, DateTimeOffset CreatedAt);