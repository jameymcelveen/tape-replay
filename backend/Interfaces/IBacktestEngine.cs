using TapeReplay.Api.Models;

namespace TapeReplay.Api.Interfaces;

/// <summary>
/// Replays historical bars and produces trade results.
/// </summary>
public interface IBacktestEngine
{
    BacktestResult Run(string ticker, DateOnly date, StrategyConfig config, IReadOnlyList<Candle> bars);
}
