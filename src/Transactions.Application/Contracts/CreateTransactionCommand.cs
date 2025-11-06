namespace Transactions.Application.Contracts;

public sealed record CreateTransactionCommand(Guid SourceAccountId, Guid TargetAccountId, int TransferTypeId, decimal Value, string IdempotencyKey);
