using Transactions.Application.Abstractions;

namespace Transactions.Api.BackgroundWorkers;

public sealed class OutboxPublisherWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IEventBusProducer _bus;
    private readonly IConfiguration _cfg;
    private readonly ILogger<OutboxPublisherWorker> _log;

    public OutboxPublisherWorker(IServiceScopeFactory scopeFactory,
                                 IEventBusProducer bus,
                                 IConfiguration cfg,
                                 ILogger<OutboxPublisherWorker> log)
    {
        _scopeFactory = scopeFactory;
        _bus = bus;
        _cfg = cfg;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        var topic = _cfg["Kafka:TopicCreated"]!;
        var batch = int.Parse(_cfg["Outbox:BatchSize"] ?? "200");
        var waitMs = int.Parse(_cfg["Outbox:PollIntervalMs"] ?? "200");

        while (!ct.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var outbox = scope.ServiceProvider.GetRequiredService<IOutboxStore>();

                var items = await outbox.DequeuePendingAsync(batch, ct);
                if (items.Count == 0)
                {
                    await Task.Delay(waitMs, ct);
                    continue;
                }

                foreach (var it in items)
                {
                    await _bus.PublishAsync(topic, it.AggregateId.ToString(), it.Payload, ct);
                    await outbox.MarkSentAsync(it.Id, ct);
                }
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "OutboxPublisher error");
                await Task.Delay(500, ct);
            }
        }
    }
}
