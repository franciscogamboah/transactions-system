using Antifraud.Mock.Config;
using Antifraud.Mock.Domain.Model;
using Antifraud.Mock.Domain.Services;
using Antifraud.Mock.Application.Abstractions; // IDailyTotalsStore, IClock

namespace Antifraud.Mock.Domain.Policies;

public sealed class RulesEvaluator : IAntifraudPolicy
{
    private readonly IDailyTotalsStore _store;
    private readonly IClock _clock;
    private readonly decimal _perTxnLimit;
    private readonly decimal _dailyCap;

    public RulesEvaluator(IDailyTotalsStore store, IClock clock, RulesOptions opt)
    {
        _store = store;
        _clock = clock;
        _perTxnLimit = opt.PerTxnLimit;
        _dailyCap = opt.DailyCap;
    }

    public Decision Decide(string sourceAccountId, decimal value, System.DateTime createdAtUtc)
    {
        if (value > _perTxnLimit) return new(DecisionStatus.Rejected, "amount_limit");

        var dayUtc = createdAtUtc == default ? _clock.UtcNow : createdAtUtc;
        var acc = _store.GetTotal(sourceAccountId, dayUtc);
        if (acc + value > _dailyCap) return new(DecisionStatus.Rejected, "daily_cap");

        _store.Add(sourceAccountId, dayUtc, value);
        return new(DecisionStatus.Approved, "ok");
    }
}
