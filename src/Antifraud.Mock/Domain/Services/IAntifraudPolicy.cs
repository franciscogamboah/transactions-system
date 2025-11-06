using Antifraud.Mock.Domain.Model;

namespace Antifraud.Mock.Domain.Services;

public interface IAntifraudPolicy
{
    Decision Decide(string sourceAccountId, decimal value, System.DateTime createdAtUtc);
}
