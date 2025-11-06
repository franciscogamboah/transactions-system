using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Antifraud.Mock.Application.Abstractions;
using Antifraud.Mock.Application.Models;
using Antifraud.Mock.Config;
using Confluent.Kafka;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Antifraud.Mock.Infrastructure.Messaging;

public sealed class KafkaCreatedConsumer : ICreatedEventsConsumer, IDisposable
{
    private readonly ILogger<KafkaCreatedConsumer> _log;
    private readonly IConsumer<string, string> _consumer;
    private readonly string _topic;
    private readonly JsonSerializerOptions _json = new() { PropertyNameCaseInsensitive = true };

    public KafkaCreatedConsumer(IOptions<KafkaOptions> opt, ILogger<KafkaCreatedConsumer> log)
    {
        _log = log;
        var o = opt.Value;

        var cc = new ConsumerConfig
        {
            BootstrapServers = o.BootstrapServers,
            GroupId = o.GroupId,
            EnableAutoCommit = o.EnableAutoCommit,
            AutoOffsetReset = Enum.TryParse<AutoOffsetReset>(o.AutoOffsetReset, true, out var rst) ? rst : AutoOffsetReset.Earliest
        };
        _consumer = new ConsumerBuilder<string, string>(cc).Build();
        _topic = o.TopicCreated;

        _consumer.Subscribe(_topic);
        _log.LogInformation("Subscribed to {topic} on {bs}", _topic, o.BootstrapServers);
    }

    public Task<ReceivedCreated?> ConsumeOneAsync(CancellationToken ct)
    {
        var cr = _consumer.Consume(TimeSpan.FromMilliseconds(500));
        if (cr is null) return Task.FromResult<ReceivedCreated?>(null);

        try
        {
            var ev = JsonSerializer.Deserialize<CreatedEvent>(cr.Message.Value ?? "{}", _json);
            if (ev is null || string.IsNullOrWhiteSpace(ev.transactionExternalId))
                throw new FormatException("Invalid created payload");

            return Task.FromResult<ReceivedCreated?>(new ReceivedCreated(
                ev,
                CommitAsync: () => { _consumer.Commit(cr); return Task.CompletedTask; }
            ));
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Invalid JSON, committing to avoid block. Payload={payload}", cr.Message.Value);
            _consumer.Commit(cr); // descartar para no bloquear
            return Task.FromResult<ReceivedCreated?>(null);
        }
    }

    public void Dispose()
    {
        try { _consumer.Close(); _consumer.Dispose(); } catch { /* ignore */ }
    }
}
