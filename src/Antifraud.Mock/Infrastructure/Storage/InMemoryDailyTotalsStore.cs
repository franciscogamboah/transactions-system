using System;
using System.Collections.Concurrent;
using Antifraud.Mock.Application.Abstractions;

namespace Antifraud.Mock.Infrastructure.Storage;

public sealed class InMemoryDailyTotalsStore : IDailyTotalsStore
{
    private static string DayKey(DateTime d) => d.ToUniversalTime().ToString("yyyy-MM-dd");
    private readonly ConcurrentDictionary<(string Source, string Day), decimal> _totals = new();

    public decimal GetTotal(string sourceAccountId, DateTime whenUtc)
        => _totals.GetValueOrDefault((sourceAccountId, DayKey(whenUtc)), 0m);

    public void Add(string sourceAccountId, DateTime whenUtc, decimal value)
    {
        var k = (sourceAccountId, DayKey(whenUtc));
        _totals.AddOrUpdate(k, value, (_, old) => old + value);
    }
}
