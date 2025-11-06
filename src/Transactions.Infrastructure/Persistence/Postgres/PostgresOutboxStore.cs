using Dapper;
using Npgsql;
using Transactions.Application.Abstractions;

namespace Transactions.Infrastructure.Persistence;

public sealed class PostgresOutboxStore : IOutboxStore
{
    private readonly NpgsqlDataSource _ds;
    public PostgresOutboxStore(NpgsqlDataSource ds) => _ds = ds;

    public async Task EnqueueAsync(Guid aggregateId, string eventType, string payload, CancellationToken ct)
    {
        await using var c = await _ds.OpenConnectionAsync(ct);

        const string sql = @"
            INSERT INTO public.outbox (aggregate_id, event_type, payload, status, created_at)
            VALUES (@a, @t, CAST(@p AS jsonb), 'pending', NOW());
        ";

        await c.ExecuteAsync(sql, new
        {
            a = aggregateId,
            t = eventType,
            p = payload
        });
    }

    public async Task<IReadOnlyList<OutboxItem>> DequeuePendingAsync(int batch, CancellationToken ct)
    {
        await using var c = await _ds.OpenConnectionAsync(ct);

        const string sql = @"
            SELECT
                id                AS Id,           -- uuid
                aggregate_id      AS AggregateId,  -- uuid
                event_type        AS EventType,    -- text
                payload::text     AS Payload,      -- jsonb → text (seguro para Dapper)
                created_at        AS CreatedAt     -- timestamptz
            FROM public.outbox
            WHERE status = 'pending'
            ORDER BY COALESCE(next_attempt_at, NOW()), created_at
            LIMIT @b
            FOR UPDATE SKIP LOCKED;
        ";

        // Mapea con tipos exactos (tupla o DTO; aquí tupla):
        var rows = await c.QueryAsync<(Guid Id, Guid AggregateId, string EventType, string Payload, DateTimeOffset CreatedAt)>(
            sql, new { b = batch });

        return rows
            .Select(r => new OutboxItem(r.Id, r.AggregateId, r.EventType, r.Payload, r.CreatedAt))
            .ToList();
    }

    public async Task MarkSentAsync(Guid id, CancellationToken ct)
    {
        await using var c = await _ds.OpenConnectionAsync(ct);
        await c.ExecuteAsync("UPDATE outbox SET status='sent', sent_at = now() WHERE id=@id;", new { id });
    }
}
