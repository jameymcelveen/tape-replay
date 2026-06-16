using TapeReplay.Api.Models;

namespace TapeReplay.Api.Interfaces;

/// <summary>
/// Replays historical bars and produces trade results.
/// </summary>
public interface IBacktestEngine
{
    BacktestResult Run(
        string ticker,
        DateOnly date,
        StrategyConfig config,
        IReadOnlyList<Candle> bars,
        TradeCostConfig costs,
        SampleLabel sampleLabel = SampleLabel.Exploratory,
        decimal startingCapitalUsd = 25_000m);

    BacktestWindowResult RunWindow(
        string ticker,
        DateOnly startDate,
        DateOnly endDate,
        StrategyConfig config,
        IReadOnlyDictionary<DateOnly, IReadOnlyList<Candle>> barsByDate,
        TradeCostConfig costs,
        SampleLabel sampleLabel,
        decimal startingCapitalUsd = 25_000m);
}
