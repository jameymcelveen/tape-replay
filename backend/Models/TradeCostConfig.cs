namespace TapeReplay.Api.Models;

/// <summary>
/// Configurable trading costs applied to every fill. Defaults are pessimistic, never zero.
/// </summary>
public sealed class TradeCostConfig
{
    public decimal CommissionPerShareUsd { get; set; } = 0.005m;

    public decimal CommissionPerTradeUsd { get; set; } = 1.00m;

    public decimal SpreadBasisPoints { get; set; } = 5m;

    public decimal SlippageBasisPoints { get; set; } = 2m;
}
