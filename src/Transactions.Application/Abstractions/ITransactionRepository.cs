using Transactions.Domain;

namespace Transactions.Application.Abstractions;

public interface ITransactionRepository
{
    Task InsertAsync(Transaction tx, CancellationToken ct);
    Task<Transaction?> GetByExternalIdAsync(Guid id, CancellationToken ct);
    Task<int> UpdateStatusAsync(Guid id, TransactionStatus status, CancellationToken ct);
}