using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TapeReplay.Api.Interfaces;
using TapeReplay.Api.Models.DataDistribution;
using TapeReplay.Api.Services;
using TapeReplay.Api.Services.DataDistribution;

namespace TapeReplay.Api.Tests.Helpers;

internal static class TestMarketDataServiceFactory
{
    public static MarketDataService Create(
        IMarketDataRepository repository,
        IMarketDataProvider provider,
        DataDistributionRole role = DataDistributionRole.Publisher)
    {
        var options = Options.Create(new DataDistributionOptions
        {
            Role = role,
            ScraperEnabled = true,
            SyncOnLaunch = false
        });

        var coverage = new NoOpCoverageRepository();
        var daily = new NoOpMarketDailyRepository();
        var scraper = new MarketDataScraperService(
            options,
            coverage,
            repository,
            daily,
            provider,
            NullLogger<MarketDataScraperService>.Instance);
        var subscriber = new DataSubscriberService(
            options,
            new NoOpPartitionStateRepository(),
            new PartitionImportService(repository, daily, coverage, new NoOpPartitionStateRepository(), NullLogger<PartitionImportService>.Instance),
            new NoHttpClientFactory(),
            NullLogger<DataSubscriberService>.Instance);

        return new MarketDataService(
            repository,
            provider,
            coverage,
            options,
            scraper,
            subscriber,
            NullLogger<MarketDataService>.Instance);
    }

    private sealed class NoOpCoverageRepository : ICoverageRepository
    {
        public Task<bool> IsMinuteDoneAsync(string ticker, DateOnly date, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task<bool> IsDailyDoneAsync(DateOnly date, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task EnsureMinutePendingAsync(string ticker, DateOnly date, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<IReadOnlyList<TickerMinuteCoverageEntity>> GetPendingMinuteCellsAsync(int limit, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<TickerMinuteCoverageEntity>>([]);

        public Task MarkMinuteDoneAsync(string ticker, DateOnly date, CoverageProvenance provenance, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task MarkDailyDoneAsync(DateOnly date, CoverageProvenance provenance, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task MarkMinuteRangeDoneFromBarsAsync(IReadOnlyList<string> tickers, DateOnly startDate, DateOnly endDate, CoverageProvenance provenance, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<IReadOnlyList<TickerMinuteCoverageEntity>> GetMinuteCoverageAsync(string? ticker, DateOnly? startDate, DateOnly? endDate, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<TickerMinuteCoverageEntity>>([]);

        public Task<IReadOnlyList<MarketDailyCoverageEntity>> GetDailyCoverageAsync(DateOnly? startDate, DateOnly? endDate, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<MarketDailyCoverageEntity>>([]);
    }

    private sealed class NoOpMarketDailyRepository : IMarketDailyRepository
    {
        public Task<IReadOnlyList<DailyBar>> GetDailyBarsForPartitionAsync(int year, int month, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<DailyBar>>([]);

        public Task UpsertDailyBarsAsync(IReadOnlyList<DailyBar> bars, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task UpsertDailyFromMinuteBarsAsync(IReadOnlyList<Models.Candle> minuteBars, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class NoOpPartitionStateRepository : IDataPartitionStateRepository
    {
        public Task<string?> GetImportedHashAsync(PartitionKind kind, string partitionKey, CancellationToken cancellationToken = default) =>
            Task.FromResult<string?>(null);

        public Task SetImportedHashAsync(PartitionKind kind, string partitionKey, string sha256, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<string?> GetPublishedHashAsync(PartitionKind kind, string partitionKey, CancellationToken cancellationToken = default) =>
            Task.FromResult<string?>(null);

        public Task SetPublishedHashAsync(PartitionKind kind, string partitionKey, string sha256, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<IReadOnlyDictionary<string, string>> GetAllImportedHashesAsync(PartitionKind kind, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyDictionary<string, string>>(new Dictionary<string, string>());
    }

    private sealed class NoHttpClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new();
    }
}
