using Antifraud.Mock.Application.Abstractions;
using Antifraud.Mock.Application.UseCases;
using Antifraud.Mock.Config;
using Antifraud.Mock.Domain.Policies;
using Antifraud.Mock.Domain.Services;
using Antifraud.Mock.Infrastructure.Hosting;
using Antifraud.Mock.Infrastructure.Messaging;
using Antifraud.Mock.Infrastructure.Storage;
using Antifraud.Mock.Infrastructure.Time;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = Host.CreateApplicationBuilder(args);

// config (appsettings.json)
// Try a few candidate locations so running `dotnet run --project` from the repo root
// still finds the project's appsettings.json.
var candidates = new[] {
    Path.Combine(builder.Environment.ContentRootPath, "appsettings.json"),
    Path.Combine(AppContext.BaseDirectory, "appsettings.json"),
    Path.Combine(AppContext.BaseDirectory, "..", "appsettings.json"),
    Path.Combine(AppContext.BaseDirectory, "..", "..", "appsettings.json")
};

var found = candidates.FirstOrDefault(File.Exists);
if (found is null)
{
    // Try to discover the project's appsettings.json by searching the repo for it,
    // prefer the one under src/Antifraud.Mock if present.
    try
    {
        var all = Directory.GetFiles(builder.Environment.ContentRootPath, "appsettings.json", SearchOption.AllDirectories);
        found = all.FirstOrDefault(f => f.Replace("\\", "/").Contains("/src/Antifraud.Mock/"))
                ?? all.FirstOrDefault();
    }
    catch
    {
        // ignore IO errors and fallthrough
    }
}

if (found is null)
{
    // If still not found, load optional so app can start with defaults and fail later with clearer message.
    builder.Configuration.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);
}
else
{
    builder.Configuration.AddJsonFile(found, optional: false, reloadOnChange: true);
}

// options
builder.Services.Configure<KafkaOptions>(builder.Configuration.GetSection("Kafka"));
builder.Services.Configure<RulesOptions>(builder.Configuration.GetSection("Rules"));
builder.Services.AddSingleton(sp => sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<RulesOptions>>().Value);

// logging
builder.Services.AddLogging(b => b.AddConsole());

// app abstractions
builder.Services.AddSingleton<IDailyTotalsStore, InMemoryDailyTotalsStore>();
builder.Services.AddSingleton<IClock, SystemClock>();

// domain policy
builder.Services.AddSingleton<IAntifraudPolicy, RulesEvaluator>();

// messaging (infra)
builder.Services.AddSingleton<ICreatedEventsConsumer, KafkaCreatedConsumer>();
builder.Services.AddSingleton<IValidatedEventsProducer, KafkaValidatedProducer>();

// use case
builder.Services.AddSingleton<ProcessCreatedEventHandler>();

// worker
builder.Services.AddHostedService<AntifraudWorker>();

var app = builder.Build();
await app.RunAsync();
