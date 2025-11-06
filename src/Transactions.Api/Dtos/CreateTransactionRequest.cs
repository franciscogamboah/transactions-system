namespace Transactions.Api.Dtos;

public sealed record CreateTransactionRequest(Guid SourceAccountId, Guid TargetAccountId, int TransferTypeId, decimal Value);