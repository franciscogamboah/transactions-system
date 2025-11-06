using System.Threading;
using System.Threading.Tasks;
using Antifraud.Mock.Application.Models;

namespace Antifraud.Mock.Application.Abstractions;

public sealed record ReceivedCreated(CreatedEvent Event, System.Func<System.Threading.Tasks.Task> CommitAsync);

public interface ICreatedEventsConsumer
{
    /// Consume UN mensaje si existe; null si no hay en este poll.
    Task<ReceivedCreated?> ConsumeOneAsync(CancellationToken ct);
}
