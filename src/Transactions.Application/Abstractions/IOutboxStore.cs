namespace Transactions.Application.Abstractions;

public interface IOutboxStore
{
    Task EnqueueAsync(Guid aggregateId, string eventType, string payload, CancellationToken ct);
    Task<IReadOnlyList<OutboxItem>> DequeuePendingAsync(int batch, CancellationToken ct);
    Task MarkSentAsync(Guid id, CancellationToken ct);
}