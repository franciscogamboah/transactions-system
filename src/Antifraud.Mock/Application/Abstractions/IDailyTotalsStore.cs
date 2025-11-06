using System;

namespace Antifraud.Mock.Application.Abstractions;

public interface IDailyTotalsStore
{
    decimal GetTotal(string sourceAccountId, DateTime whenUtc);
    void Add(string sourceAccountId, DateTime whenUtc, decimal value);
}
