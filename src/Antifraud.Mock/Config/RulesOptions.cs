namespace Antifraud.Mock.Config;

public sealed class RulesOptions
{
    public decimal PerTxnLimit { get; set; } = 2500m;
    public decimal DailyCap { get; set; } = 20500m;
}
