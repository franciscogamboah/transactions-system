using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Antifraud.Mock.Application.Abstractions;
using Antifraud.Mock.Application.Models;
using Antifraud.Mock.Domain.Model;
using Antifraud.Mock.Domain.Services;
using Microsoft.Extensions.Logging;

namespace Antifraud.Mock.Application.UseCases;

public sealed class ProcessCreatedEventHandler
{
    private readonly ICreatedEventsConsumer _consumer;
    private readonly IValidatedEventsProducer _producer;
    private readonly IAntifraudPolicy _policy;
    private readonly ILogger<ProcessCreatedEventHandler> _log;

    public ProcessCreatedEventHandler(
        ICreatedEventsConsumer consumer,
        IValidatedEventsProducer producer,
        IAntifraudPolicy policy,
        ILogger<ProcessCreatedEventHandler> log)
    {
        _consumer = consumer;
        _producer = producer;
        _policy = policy;
        _log = log;
    }

    public async Task<bool> ProcessOneAsync(CancellationToken ct)
    {
        var msg = await _consumer.ConsumeOneAsync(ct);
        if (msg is null) return false; // no hay mensaje en este poll

        var ev = msg.Event;

        Decision decision;
        try
        {
            decision = _policy.Decide(ev.sourceAccountId, ev.value, ev.createdAt);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Policy error (discarding) for {tx}", ev.transactionExternalId);
            await msg.CommitAsync(); // evitamos bloquearnos con un mal payload
            return true;
        }

        var validated = new ValidatedEvent(
            ev.transactionExternalId,
            decision.Status == DecisionStatus.Approved ? "approved" : "rejected",
            decision.Reason,
            DateTime.UtcNow
        );

        // produce y si sale bien, confirmamos el consumo
        await _producer.ProduceAsync(validated, ev.transactionExternalId, ct);
        await msg.CommitAsync();

        _log.LogInformation("Validated {tx} => {status} ({reason})",
            ev.transactionExternalId, validated.status, validated.reason);

        return true;
    }
}
