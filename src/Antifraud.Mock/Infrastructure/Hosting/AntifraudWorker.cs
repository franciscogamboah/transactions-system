using System.Threading;
using System.Threading.Tasks;
using Antifraud.Mock.Application.UseCases;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Antifraud.Mock.Infrastructure.Hosting;

public sealed class AntifraudWorker : BackgroundService
{
    private readonly ILogger<AntifraudWorker> _log;
    private readonly ProcessCreatedEventHandler _handler;

    public AntifraudWorker(ILogger<AntifraudWorker> log, ProcessCreatedEventHandler handler)
    { _log = log; _handler = handler; }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        _log.LogInformation("AntifraudWorker started");
        while (!ct.IsCancellationRequested)
        {
            var got = await _handler.ProcessOneAsync(ct);
            if (!got) await Task.Delay(200, ct); // backoff si no hubo mensaje
        }
    }
}
