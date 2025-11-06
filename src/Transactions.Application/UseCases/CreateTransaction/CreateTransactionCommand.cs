namespace Transactions.Application.UserCases.CreateTransaction;

public sealed record CreateTransactionCommand(Guid SourceAccountId, Guid TargetAccountId, int TransferTypeId, decimal Value, string IdempotencyKey);