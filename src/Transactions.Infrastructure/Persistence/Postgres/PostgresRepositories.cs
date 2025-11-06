using Dapper;
using Microsoft.Extensions.Logging;
using Npgsql;
using Transactions.Application.Abstractions;
using Transactions.Domain;

public sealed class TxRow
{
    public Guid ExternalId { get; set; }
    public Guid SourceAccountId { get; set; }
    public Guid TargetAccountId { get; set; }
    public int TransferTypeId { get; set; }
    public decimal Value { get; set; }
    public string Status { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class PostgresTransactionRepository : ITransactionRepository
{
    private readonly NpgsqlDataSource _ds;
    private readonly ILogger<PostgresTransactionRepository> _log;

    public PostgresTransactionRepository(NpgsqlDataSource ds, ILogger<PostgresTransactionRepository> log)
    {
        _ds = ds;
        _log = log;
    }

    public async Task InsertAsync(Transaction tx, CancellationToken ct)
    {
        await using var c = await _ds.OpenConnectionAsync(ct);
        const string sql = @"
        INSERT INTO public.transactions
            (external_id, source_account_id, target_account_id, transfer_type_id, value, status, created_at, updated_at, idempotency_key)
        VALUES
            (@ExternalId, @SourceAccountId, @TargetAccountId, @TransferTypeId, @Value, @Status, @CreatedAt, @UpdatedAt, NULL);";

        var cmd = new CommandDefinition(sql, new
        {
            tx.ExternalId,
            tx.SourceAccountId,
            tx.TargetAccountId,
            tx.TransferTypeId,
            tx.Value,
            Status = tx.Status.ToString().ToLowerInvariant(),
            tx.CreatedAt,
            tx.UpdatedAt
        }, cancellationToken: ct);

        await c.ExecuteAsync(cmd);
    }

    public async Task<Transaction?> GetByExternalIdAsync(Guid id, CancellationToken ct)
    {
        await using var c = await _ds.OpenConnectionAsync(ct);
        const string sql = @"
        SELECT
            external_id        AS ""ExternalId"",
            source_account_id  AS ""SourceAccountId"",
            target_account_id  AS ""TargetAccountId"",
            transfer_type_id   AS ""TransferTypeId"",
            value              AS ""Value"",
            status             AS ""Status"",
            -- Forzamos TZ para mapear a DateTimeOffset de forma estable
            (created_at AT TIME ZONE 'UTC')  AS ""CreatedAt"",
            (updated_at AT TIME ZONE 'UTC')  AS ""UpdatedAt""
        FROM public.transactions
        WHERE external_id = @id::uuid
        LIMIT 1;";

        var row = await c.QuerySingleOrDefaultAsync<TxRow>(sql, new { id });
        if (row is null) return null;

        var status = Enum.Parse<TransactionStatus>(row.Status, ignoreCase: true);

        return Transaction.Restore(
            row.ExternalId,
            row.SourceAccountId,
            row.TargetAccountId,
            row.TransferTypeId,
            row.Value,
            status,
            row.CreatedAt,
            row.UpdatedAt
        );
    }


    public async Task<int> UpdateStatusAsync(Guid id, TransactionStatus status, CancellationToken ct)
    {
        await using var c = await _ds.OpenConnectionAsync(ct);
        const string sql = @"
            UPDATE public.transactions
            SET status = @s, updated_at = NOW()
            WHERE external_id = @id::uuid;";

        var cmd = new CommandDefinition(sql, new { id, s = status.ToString().ToLowerInvariant() }, cancellationToken: ct);
        return await c.ExecuteAsync(cmd);
    }
}