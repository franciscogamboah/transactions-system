using Microsoft.AspNetCore.Mvc;
using Transactions.Application.UserCases.CreateTransaction;
using Transactions.Application.Abstractions;
using Transactions.Api.Dtos;

namespace Transactions.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
// [Authorize]  // JWT
public sealed class TransactionsController : ControllerBase
{
    private readonly CreateTransactionHandler _createHandler;
    private readonly ITransactionRepository _repo;

    public TransactionsController(CreateTransactionHandler createHandler, ITransactionRepository repo)
    {
        _createHandler = createHandler;
        _repo = repo;
    }

    /// <summary>Crea una transacción en estado 'pending' y encola evento outbox.</summary>
    [HttpPost]
    [Consumes("application/json")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(CreateTransactionResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(
        [FromBody] CreateTransactionRequest body,
        CancellationToken ct)
    {
        var idem = Request.Headers["Idempotency-Key"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(idem))
            return BadRequest(new { error = "Idempotency-Key header required" });

        var result = await _createHandler.HandleAsync(
            new CreateTransactionCommand(body.SourceAccountId, body.TargetAccountId, body.TransferTypeId, body.Value, idem),
            ct);

        var response = new CreateTransactionResponse(
            result.TransactionExternalId, result.Status, result.CreatedAt);

        return CreatedAtAction(nameof(GetById), new { id = response.TransactionExternalId }, response);
    }

    /// <summary>Obtiene una transacción por su externalId.</summary>
    [HttpGet("{id:guid}")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(GetTransactionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById([FromRoute] Guid id, CancellationToken ct)
    {
        var tx = await _repo.GetByExternalIdAsync(id, ct);
        if (tx is null) return NotFound();

        var response = new GetTransactionResponse(
            tx.ExternalId,
            tx.Status.ToString().ToLowerInvariant(),
            tx.CreatedAt
        );
        return Ok(response);
    }
}
