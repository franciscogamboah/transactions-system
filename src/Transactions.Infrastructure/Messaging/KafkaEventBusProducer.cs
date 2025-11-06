using Confluent.Kafka;
using Transactions.Application.Abstractions;

namespace Transactions.Infrastructure.Messaging;

public sealed class KafkaEventBusProducer : IEventBusProducer, IDisposable
{
    private readonly IProducer<string, string> _producer;

    public KafkaEventBusProducer(string bootstrapServers)
    {
        var cfg = new ProducerConfig
        {
            BootstrapServers = bootstrapServers,
            Acks = Acks.All,
            EnableIdempotence = true
        };
        _producer = new ProducerBuilder<string, string>(cfg).Build();
    }

    public async Task PublishAsync(string topic, string key, string payload, CancellationToken ct)
    {
        var msg = new Message<string, string> { Key = key, Value = payload };
        // ProduceAsync YA es un Task<DeliveryResult<...>>; simplemente hacemos await.
        await _producer.ProduceAsync(topic, msg, ct);
        // (Opcional) logging:
        // var result = await _producer.ProduceAsync(topic, msg, ct);
        // _log?.LogDebug("Delivered to {TopicPartitionOffset}", result.TopicPartitionOffset);
    }

    public void Dispose() => _producer.Dispose();
}
