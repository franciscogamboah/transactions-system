using Confluent.Kafka;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;
using Transactions.Api.BackgroundWorkers; // ValidatedEventProcessor (scoped)

public sealed class StatusConsumerWorker : BackgroundService
{
    private readonly ILogger<StatusConsumerWorker> _log;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _cfg;

    public StatusConsumerWorker(
        ILogger<StatusConsumerWorker> log,
        IServiceScopeFactory scopeFactory,
        IConfiguration cfg)
    {
        _log = log;
        _scopeFactory = scopeFactory;
        _cfg = cfg;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        var bootstrap = _cfg["Kafka:BootstrapServers"] ?? "localhost:9092";
        var topic = _cfg["Kafka:TopicValidated"] ?? "transactions.validated.v1";

        // backoff incremental para reconectar si falla construir/suscribir el consumer
        var backoff = new[] { 500, 1000, 2000, 5000, 5000, 5000 };

        while (!ct.IsCancellationRequested)
        {
            try
            {
                var config = new ConsumerConfig
                {
                    GroupId = _cfg["Kafka:GroupId"] ?? "status-worker",
                    BootstrapServers = bootstrap,
                    EnableAutoCommit = true,
                    AutoOffsetReset = AutoOffsetReset.Earliest,
                    AllowAutoCreateTopics = true,
                    // Opcional: timeouts más cortos para reaccionar rápido a caídas
                    // SessionTimeoutMs = 10000,
                    // SocketTimeoutMs  = 10000,
                };

                using var consumer = new ConsumerBuilder<Ignore, string>(config).Build();
                consumer.Subscribe(topic);
                _log.LogInformation("StatusConsumer subscribed to {Topic} on {Bootstrap}", topic, bootstrap);

                while (!ct.IsCancellationRequested)
                {
                    try
                    {
                        // Consume bloqueante, sale por mensaje o por cancelación
                        var cr = consumer.Consume(ct);
                        if (cr is null) continue;

                        var payload = cr.Message?.Value;
                        if (string.IsNullOrWhiteSpace(payload))
                        {
                            _log.LogWarning("Empty payload at {Offset}; skipping.", cr.Offset);
                            continue;
                        }

                        using var scope = _scopeFactory.CreateScope();
                        var processor = scope.ServiceProvider.GetRequiredService<ValidatedEventProcessor>();

                        var ok = await processor.ProcessAsync(payload, ct);
                        if (!ok)
                            _log.LogWarning("Event at {Offset} was not processed", cr.Offset);
                    }
                    catch (ConsumeException cex)
                    {
                        // Errores de broker/red: log y seguimos; no tumbar el host
                        _log.LogError(cex, "Kafka consume error; will retry");
                        await Task.Delay(500, ct);
                    }
                    catch (OperationCanceledException)
                    {
                        break; // shutdown solicitado
                    }
                    catch (Exception ex)
                    {
                        _log.LogError(ex, "Unexpected error in StatusConsumer loop; continuing");
                        await Task.Delay(500, ct);
                    }
                }

                // Cierre limpio del consumer al terminar el loop o cancelar
                try { consumer.Close(); } catch { /* ignore */ }
            }
            catch (OperationCanceledException)
            {
                break; // cierre del host
            }
            catch (Exception ex)
            {
                // Falla al construir o suscribir: reintentar con backoff sin matar el host
                _log.LogError(ex, "Kafka connection/subscription failed; retrying with backoff");
                foreach (var ms in backoff)
                {
                    if (ct.IsCancellationRequested) break;
                    try { await Task.Delay(ms, ct); } catch { /* cancelled */ }
                }
            }
        }
    }
}
