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

public sealed class KafkaValidatedProducer : IValidatedEventsProducer, System.IDisposable
{
    private readonly ILogger<KafkaValidatedProducer> _log;
    private readonly IProducer<string, string> _producer;
    private readonly string _topic;

    public KafkaValidatedProducer(IOptions<KafkaOptions> opt, ILogger<KafkaValidatedProducer> log)
    {
        _log = log;
        var o = opt.Value;
        var pc = new ProducerConfig { BootstrapServers = o.BootstrapServers, Acks = Acks.All, LingerMs = 5 };
        _producer = new ProducerBuilder<string, string>(pc).Build();
        _topic = o.TopicValidated;
    }

    public async Task ProduceAsync(ValidatedEvent ev, string key, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(ev);
        var dr = await _producer.ProduceAsync(_topic, new Message<string, string> { Key = key, Value = json }, ct);
        _log.LogInformation("Produced validated to {topic} @ {off} key={key}", _topic, dr.Offset, key);
    }

    public void Dispose()
    {
        try { _producer.Flush(System.TimeSpan.FromSeconds(2)); _producer.Dispose(); } catch { /* ignore */ }
    }
}
