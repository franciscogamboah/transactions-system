using System.Threading;
using System.Threading.Tasks;
using Antifraud.Mock.Application.Models;

namespace Antifraud.Mock.Application.Abstractions;

public interface IValidatedEventsProducer
{
    Task ProduceAsync(ValidatedEvent ev, string key, CancellationToken ct);
}
