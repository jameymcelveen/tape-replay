using TapeReplay.Api.Models;

namespace TapeReplay.Api.Interfaces;

/// <summary>
/// Applies pessimistic commission, spread, and slippage to every fill.
/// </summary>
public interface ITradeCostModel
{
    FillCost CalculateEntry(decimal referencePrice, int shares, TradeCostConfig config);

    FillCost CalculateExit(decimal referencePrice, int shares, TradeCostConfig config);
}
