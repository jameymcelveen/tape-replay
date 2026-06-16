using TapeReplay.Api.Interfaces;
using TapeReplay.Api.Models;

namespace TapeReplay.Api.Services;

/// <summary>
/// Pessimistic default cost model: buy at ask, sell at bid, plus slippage and commission on every fill.
/// </summary>
public sealed class TradeCostModel : ITradeCostModel
{
    public FillCost CalculateEntry(decimal referencePrice, int shares, TradeCostConfig config)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(shares);
        ArgumentNullException.ThrowIfNull(config);

        var halfSpread = referencePrice * (config.SpreadBasisPoints / 20_000m);
        var slippage = referencePrice * (config.SlippageBasisPoints / 10_000m);
        var fillPrice = referencePrice + halfSpread + slippage;
        var spreadCost = halfSpread * shares;
        var slippageCost = slippage * shares;
        var commission = (config.CommissionPerShareUsd * shares) + config.CommissionPerTradeUsd;

        return new FillCost
        {
            ReferencePrice = referencePrice,
            FillPrice = fillPrice,
            Shares = shares,
            Commission = commission,
            SpreadCost = spreadCost,
            SlippageCost = slippageCost,
            TotalCost = spreadCost + slippageCost + commission
        };
    }

    public FillCost CalculateExit(decimal referencePrice, int shares, TradeCostConfig config)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(shares);
        ArgumentNullException.ThrowIfNull(config);

        var halfSpread = referencePrice * (config.SpreadBasisPoints / 20_000m);
        var slippage = referencePrice * (config.SlippageBasisPoints / 10_000m);
        var fillPrice = referencePrice - halfSpread - slippage;
        var spreadCost = halfSpread * shares;
        var slippageCost = slippage * shares;
        var commission = (config.CommissionPerShareUsd * shares) + config.CommissionPerTradeUsd;

        return new FillCost
        {
            ReferencePrice = referencePrice,
            FillPrice = fillPrice,
            Shares = shares,
            Commission = commission,
            SpreadCost = spreadCost,
            SlippageCost = slippageCost,
            TotalCost = spreadCost + slippageCost + commission
        };
    }
}
