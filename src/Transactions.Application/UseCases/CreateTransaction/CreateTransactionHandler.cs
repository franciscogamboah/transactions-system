using System.Text.Json;
using Transactions.Domain;
using Transactions.Application.Abstractions;

namespace Transactions.Application.UserCases.CreateTransaction;

public sealed class CreateTransactionHandler
{
    private readonly ITransactionRepository _repo;
    private readonly IOutboxStore _outbox;

    public CreateTransactionHandler(ITransactionRepository repo, IOutboxStore outbox)
    { _repo = repo; _outbox = outbox; }

    public async Task<CreateTransactionResult> HandleAsync(CreateTransactionCommand cmd, CancellationToken ct)
    {
        // Idempotencia persistente se puede añadir luego (tabla aparte). Aquí vamos directo a la creación.
        var tx = Transaction.Create(cmd.SourceAccountId, cmd.TargetAccountId, cmd.TransferTypeId, cmd.Value);

        await _repo.InsertAsync(tx, ct);

        var payload = JsonSerializer.Serialize(new
        {
            transactionExternalId = tx.ExternalId,
            sourceAccountId = tx.SourceAccountId,
            targetAccountId = tx.TargetAccountId,
            transferTypeId = tx.TransferTypeId,
            value = tx.Value,
            createdAt = tx.CreatedAt
        });
        await _outbox.EnqueueAsync(tx.ExternalId, "transactions.created.v1", payload, ct);

        return new CreateTransactionResult(tx.ExternalId, "pending", tx.CreatedAt);
    }
}
