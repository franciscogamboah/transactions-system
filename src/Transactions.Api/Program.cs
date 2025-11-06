
using Npgsql;
using Transactions.Api.BackgroundWorkers;
using Transactions.Application.Abstractions;
using Transactions.Application.UserCases.CreateTransaction;
using Transactions.Infrastructure.Messaging;
using Transactions.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// === Fuerza Kestrel en 127.0.0.1:5313 ===
builder.WebHost.ConfigureKestrel(o => o.ListenLocalhost(5313));

// === Logging claro a consola ===
builder.Logging.ClearProviders();
builder.Logging.AddSimpleConsole(o => { o.SingleLine = true; o.TimestampFormat = "HH:mm:ss "; });

// === Controllers + Swagger ===
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// === Infra/DI (valores por defecto OK para local) ===
var cs = builder.Configuration.GetConnectionString("Postgres")
         ?? "Host=localhost;Port=5432;Database=transactionsdb;Username=postgres;Password=postgres";
builder.Services.AddSingleton(new NpgsqlDataSourceBuilder(cs).Build());
builder.Services.AddScoped<ITransactionRepository, PostgresTransactionRepository>();
builder.Services.AddScoped<IOutboxStore, PostgresOutboxStore>();
builder.Services.AddSingleton<IEventBusProducer>(_ => new KafkaEventBusProducer("localhost:9092"));
builder.Services.AddScoped<ValidatedEventProcessor>();
builder.Services.AddScoped<CreateTransactionHandler>();

// === Workers ===
builder.Services.AddHostedService<OutboxPublisherWorker>();
builder.Services.AddHostedService<StatusConsumerWorker>();

var app = builder.Build();

// === Pipeline HTTP ===
app.UseSwagger();
app.UseSwaggerUI();

app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { ok = true }));

// Log de URLs efectivas (para ver “Listening on…”)
var logger = app.Services.GetRequiredService<ILogger<Program>>();
app.Lifetime.ApplicationStarted.Register(() =>
{
    logger.LogInformation("Listening on: {Urls}", string.Join(", ", app.Urls));
});

app.Run();
