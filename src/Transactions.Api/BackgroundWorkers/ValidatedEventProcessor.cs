using System.Text.Json;
using Microsoft.Extensions.Logging;
using Transactions.Application.Abstractions;
using Transactions.Domain;

namespace Transactions.Api.BackgroundWorkers;

public sealed class ValidatedEventProcessor
{
    private readonly ITransactionRepository _repo;
    private readonly ILogger<ValidatedEventProcessor> _log;

    public ValidatedEventProcessor(ITransactionRepository repo, ILogger<ValidatedEventProcessor> log)
    {
        _repo = repo;
        _log = log;
    }

    private sealed class ValidatedEventDto
    {
        public string? TransactionExternalId { get; set; }
        public string? Status { get; set; }
        public string? Reason { get; set; }
        public DateTimeOffset? EvaluatedAt { get; set; }
    }

    public async Task<bool> ProcessAsync(string payload, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            _log.LogWarning("Empty payload in ValidatedEventProcessor.ProcessAsync");
            return false;
        }

        ValidatedEventDto? dto;
        try
        {
            dto = JsonSerializer.Deserialize<ValidatedEventDto>(payload, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Failed to deserialize validated payload");
            return false;
        }

        if (dto is null)
        {
            _log.LogWarning("Deserialized DTO is null");
            return false;
        }

        if (!Guid.TryParse(dto.TransactionExternalId, out var id))
        {
            _log.LogWarning("Invalid transactionExternalId: {Id}", dto.TransactionExternalId);
            return false;
        }

        if (!Enum.TryParse<TransactionStatus>(dto.Status ?? string.Empty, true, out var newStatus))
        {
            _log.LogWarning("Invalid status value: {Status}", dto.Status);
            return false;
        }

        var rows = await _repo.UpdateStatusAsync(id, newStatus, ct);
        _log.LogInformation("Processed validated event for {Id} -> {Status} (rows={Rows})", id, newStatus, rows);
        return rows > 0;
    }
}
