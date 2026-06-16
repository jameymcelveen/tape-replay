using TapeReplay.Api.Interfaces;
using TapeReplay.Api.Models;

namespace TapeReplay.Api.Tests.Helpers;

internal sealed class FixedBarsProvider(IReadOnlyDictionary<DateOnly, IReadOnlyList<Candle>> bars) : IMarketDataProvider
{
    public Task<IReadOnlyList<Candle>> GetMinuteBarsAsync(string ticker, DateOnly date, CancellationToken cancellationToken = default)
    {
        bars.TryGetValue(date, out var dayBars);
        return Task.FromResult<IReadOnlyList<Candle>>(dayBars ?? []);
    }
}

internal sealed class InMemoryMarketDataRepository(IReadOnlyDictionary<DateOnly, IReadOnlyList<Candle>> bars) : IMarketDataRepository
{
    public Task<bool> HasDataAsync(string ticker, DateOnly date, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(bars.ContainsKey(date));
    }

    public Task<IReadOnlyList<Candle>> GetBarsAsync(string ticker, DateOnly date, CancellationToken cancellationToken = default)
    {
        bars.TryGetValue(date, out var dayBars);
        return Task.FromResult<IReadOnlyList<Candle>>(dayBars ?? []);
    }

    public Task SaveBarsAsync(string ticker, IReadOnlyList<Candle> barsToSave, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}
