using System.Text.Json;
using Microsoft.Extensions.Options;
using TapeReplay.Api.Interfaces;
using TapeReplay.Api.Models.ChartBacktest;
using TapeReplay.Api.Models.DataDistribution;
using TapeReplay.Api.Services.DataDistribution;

namespace TapeReplay.Api.Services.ChartBacktest;

/// <summary>
/// Builds strategy performance heatmap grids with per-config caching.
/// </summary>
public sealed class StrategyHeatmapService(
    ChartBacktestService chartBacktestService,
    MarketDataService marketDataService,
    IMarketDataRepository marketDataRepository,
    IStrategyResultRepository strategyResultRepository,
    IOptions<RecordingJobOptions> recordingOptions)
{
    /// <summary>
    /// Computes or loads cached strategy results for each ticker and trading day in range.
    /// </summary>
    public async Task<StrategyHeatmapResponse> RunAsync(
        StrategyHeatmapRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.To < request.From)
        {
            throw new ArgumentException("'to' must be on or after 'from'.");
        }

        if (request.Strategy.Shares <= 0)
        {
            throw new ArgumentException("Shares must be positive.");
        }

        var configHash = StrategyConfigHasher.Compute(request.Strategy);
        var tradingDays = EnumerateTradingDays(request.From, request.To);
        var tickers = await ResolveTickersAsync(request.Tickers, cancellationToken);

        var rows = new List<StrategyHeatmapRow>(tickers.Count);
        foreach (var ticker in tickers)
        {
            var days = await BuildTickerRowAsync(
                ticker,
                tradingDays,
                request.Strategy,
                configHash,
                cancellationToken);
            rows.Add(new StrategyHeatmapRow { Ticker = ticker, Days = days });
        }

        return new StrategyHeatmapResponse
        {
            StrategyConfigHash = configHash,
            TradingDays = tradingDays,
            Rows = rows
        };
    }

    private async Task<IReadOnlyList<string>> ResolveTickersAsync(
        object? tickersValue,
        CancellationToken cancellationToken)
    {
        if (tickersValue is JsonElement element)
        {
            tickersValue = element.ValueKind switch
            {
                JsonValueKind.String => element.GetString(),
                JsonValueKind.Array => element.EnumerateArray()
                    .Where(e => e.ValueKind == JsonValueKind.String)
                    .Select(e => e.GetString() ?? string.Empty)
                    .ToList(),
                _ => throw new ArgumentException("tickers must be an array of symbols or the sentinel watchlist / all-with-data.")
            };
        }

        if (tickersValue is string sentinel)
        {
            return sentinel.Trim().ToLowerInvariant() switch
            {
                "watchlist" => ResolveWatchlistTickers(),
                "all-with-data" => await marketDataRepository.GetDistinctTickersWithMinuteDataAsync(cancellationToken),
                _ => throw new ArgumentException($"Unknown tickers sentinel '{sentinel}'. Use watchlist or all-with-data.")
            };
        }

        if (tickersValue is IEnumerable<string> explicitTickers)
        {
            var list = explicitTickers
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Select(t => t.Trim().ToUpperInvariant())
                .Distinct(StringComparer.Ordinal)
                .OrderBy(t => t, StringComparer.Ordinal)
                .ToList();

            if (list.Count == 0)
            {
                throw new ArgumentException("At least one ticker is required.");
            }

            return list;
        }

        throw new ArgumentException("tickers must be an array of symbols or the sentinel watchlist / all-with-data.");
    }

    private IReadOnlyList<string> ResolveWatchlistTickers()
    {
        var tickers = recordingOptions.Value.Jobs
            .SelectMany(j => j.Tickers)
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Select(t => t.Trim().ToUpperInvariant())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(t => t, StringComparer.Ordinal)
            .ToList();

        if (tickers.Count == 0)
        {
            throw new ArgumentException("No watchlist tickers configured in Recording:Jobs.");
        }

        return tickers;
    }

    private async Task<IReadOnlyList<StrategyHeatmapDayCell>> BuildTickerRowAsync(
        string ticker,
        IReadOnlyList<DateOnly> tradingDays,
        StrategyHeatmapStrategyConfig strategy,
        string configHash,
        CancellationToken cancellationToken)
    {
        var cached = new Dictionary<DateOnly, StrategyResultEntity>(
            await strategyResultRepository.GetCachedAsync(
                ticker,
                tradingDays[0],
                tradingDays[^1],
                configHash,
                cancellationToken));

        var missingDates = tradingDays.Where(d => !cached.ContainsKey(d)).ToList();
        var newResults = new List<StrategyResultEntity>();

        if (missingDates.Count > 0)
        {
            var (fromUtc, toUtc) = EasternMarketTime.ExtendedSessionBounds(tradingDays[0], tradingDays[^1]);
            var rawBars = await marketDataService.GetStoredMinuteBarsInRangeAsync(
                ticker,
                fromUtc,
                toUtc,
                cancellationToken);

            var barsByDay = rawBars
                .GroupBy(b => MarketSessionClassifier.GetEasternDate(b.DateTime))
                .ToDictionary(g => g.Key, g => (IReadOnlyList<Models.Candle>)g.OrderBy(b => b.DateTime).ToList());

            foreach (var date in missingDates)
            {
                barsByDay.TryGetValue(date, out var dayBars);
                dayBars ??= [];

                var evaluation = chartBacktestService.EvaluateDayBars(
                    dayBars,
                    strategy.Rule,
                    strategy.Params,
                    strategy.Shares,
                    strategy.Scope);

                var entity = ToEntity(ticker, date, configHash, evaluation);
                newResults.Add(entity);
                cached[date] = entity;
            }

            await strategyResultRepository.SaveAsync(newResults, cancellationToken);
        }

        return tradingDays
            .Select(date => ToCell(date, cached.GetValueOrDefault(date)))
            .ToList();
    }

    private static StrategyResultEntity ToEntity(
        string ticker,
        DateOnly date,
        string configHash,
        ChartDayEvaluation evaluation) => new()
        {
            Ticker = ticker,
            Date = date,
            StrategyConfigHash = configHash,
            HasData = evaluation.HasData,
            Traded = evaluation.Traded,
            PnlPct = evaluation.PnlPct,
            CapturePct = evaluation.CapturePct,
            PnlDollar = evaluation.PnlDollar,
            EntryTime = evaluation.EntryTime,
            EntryPrice = evaluation.EntryPrice,
            ExitTime = evaluation.ExitTime,
            ExitPrice = evaluation.ExitPrice,
            ExitReason = evaluation.ExitReason,
            ComputedAt = DateTime.UtcNow
        };

    private static StrategyHeatmapDayCell ToCell(DateOnly date, StrategyResultEntity? entity)
    {
        if (entity is null)
        {
            return new StrategyHeatmapDayCell { Date = date, HasData = false, Traded = false };
        }

        return new StrategyHeatmapDayCell
        {
            Date = date,
            HasData = entity.HasData,
            Traded = entity.Traded,
            PnlPct = entity.PnlPct,
            CapturePct = entity.CapturePct,
            PnlDollar = entity.PnlDollar,
            EntryTime = entity.EntryTime,
            EntryPrice = entity.EntryPrice,
            ExitTime = entity.ExitTime,
            ExitPrice = entity.ExitPrice,
            ExitReason = entity.ExitReason
        };
    }

    private static IReadOnlyList<DateOnly> EnumerateTradingDays(DateOnly from, DateOnly to)
    {
        var days = new List<DateOnly>();
        for (var date = from; date <= to; date = date.AddDays(1))
        {
            if (TradingCalendar.IsTradingDay(date))
            {
                days.Add(date);
            }
        }

        return days;
    }
}
