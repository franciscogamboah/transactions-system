namespace Transactions.Application.Abstractions;

public interface IEventBusProducer
{
    Task PublishAsync(string topic, string key, string payload, CancellationToken ct);
}