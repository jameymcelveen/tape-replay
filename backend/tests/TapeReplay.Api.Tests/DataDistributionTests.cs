using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Shouldly;
using TapeReplay.Api.Data;
using TapeReplay.Api.Models;
using TapeReplay.Api.Models.DataDistribution;
using TapeReplay.Api.Services.DataDistribution;
using TapeReplay.Api.Tests.Helpers;

namespace TapeReplay.Api.Tests;

public sealed class ParquetPartitionCodecTests
{
    [Fact]
    public async Task Minute_partition_round_trips_through_parquet_zstd()
    {
        var bars = TestCandles.CreateMinuteSeries("AAPL", new DateOnly(2024, 6, 3), 10);
        var path = Path.Combine(Path.GetTempPath(), $"minute-{Guid.NewGuid():N}.parquet");

        try
        {
            await ParquetMinutePartitionCodec.WriteAsync(path, bars);
            var roundTrip = await ParquetMinutePartitionCodec.ReadAsync(path);

            roundTrip.Count.ShouldBe(bars.Count);
            roundTrip[0].Ticker.ShouldBe("AAPL");
            roundTrip[0].Open.ShouldBe(bars[0].Open);
            roundTrip[^1].Close.ShouldBe(bars[^1].Close);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public async Task Daily_partition_round_trips_through_parquet_zstd()
    {
        var bars = new List<DailyBar>
        {
            new()
            {
                Ticker = "AAPL",
                Date = new DateOnly(2024, 6, 3),
                Open = 100m,
                High = 105m,
                Low = 99m,
                Close = 104m,
                Volume = 1_000_000
            },
            new()
            {
                Ticker = "MSFT",
                Date = new DateOnly(2024, 6, 3),
                Open = 200m,
                High = 205m,
                Low = 198m,
                Close = 203m,
                Volume = 2_000_000
            }
        };

        var path = Path.Combine(Path.GetTempPath(), $"daily-{Guid.NewGuid():N}.parquet");

        try
        {
            await ParquetDailyPartitionCodec.WriteAsync(path, bars);
            var roundTrip = await ParquetDailyPartitionCodec.ReadAsync(path);
            roundTrip.Count.ShouldBe(2);
            roundTrip[0].Ticker.ShouldBe("AAPL");
            roundTrip[1].Ticker.ShouldBe("MSFT");
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}

public sealed class DataPublisherTests
{
    [Fact]
    public async Task Publish_exports_only_changed_partitions_on_second_run()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"tapereplay-pub-{Guid.NewGuid():N}.db");
        var publishDir = Path.Combine(Path.GetTempPath(), $"tapereplay-publish-{Guid.NewGuid():N}");
        await using var dbContext = CreateDb(dbPath);

        var bars = TestCandles.CreateMinuteSeries("AAPL", new DateOnly(2024, 6, 3), 5);
        await dbContext.MarketData.AddRangeAsync(bars.Select(b => new MarketDataEntity
        {
            Ticker = b.Ticker,
            DateTime = b.DateTime,
            Open = b.Open,
            High = b.High,
            Low = b.Low,
            Close = b.Close,
            Volume = b.Volume
        }));
        await dbContext.SaveChangesAsync();

        var options = Options.Create(new DataDistributionOptions
        {
            Role = DataDistributionRole.Publisher,
            PublishDirectory = publishDir,
            IncludeBootstrapArchive = false
        });

        var publisher = new DataPublisherService(
            options,
            new MarketDataRepository(dbContext),
            new MarketDailyRepository(dbContext),
            new DataPartitionStateRepository(dbContext),
            NullLogger<DataPublisherService>.Instance);

        var first = await publisher.PublishAsync();
        first.PartitionsExported.ShouldBeGreaterThan(0);

        var parquetCountAfterFirst = Directory.GetFiles(publishDir, "*.parquet").Length;

        var second = await publisher.PublishAsync();
        second.PartitionsExported.ShouldBe(0);
        second.PartitionsSkipped.ShouldBeGreaterThan(0);
        Directory.GetFiles(publishDir, "*.parquet").Length.ShouldBe(parquetCountAfterFirst);
    }

    private static AppDbContext CreateDb(string dbPath)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={dbPath}")
            .Options;
        var db = new AppDbContext(options);
        SchemaMigrator.ApplyAsync(db).GetAwaiter().GetResult();
        return db;
    }
}

public sealed class CoverageProvenanceTests
{
    [Fact]
    public async Task Scraper_skips_cells_already_done_from_published_import()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"tapereplay-cov-{Guid.NewGuid():N}.db");
        await using var dbContext = CreateDb(dbPath);
        var coverage = new CoverageRepository(dbContext);

        await coverage.MarkMinuteDoneAsync("AAPL", new DateOnly(2024, 6, 3), CoverageProvenance.Published);
        (await coverage.IsMinuteDoneAsync("AAPL", new DateOnly(2024, 6, 3))).ShouldBeTrue();

        var provider = new CountingProvider();
        var scraper = new MarketDataScraperService(
            Options.Create(new DataDistributionOptions { Role = DataDistributionRole.Publisher, ScraperEnabled = true }),
            coverage,
            new MarketDataRepository(dbContext),
            new MarketDailyRepository(dbContext),
            provider,
            NullLogger<MarketDataScraperService>.Instance);

        await coverage.EnsureMinutePendingAsync("AAPL", new DateOnly(2024, 6, 3));
        var recorded = await scraper.ScrapePendingAsync();
        recorded.ShouldBe(0);
        provider.CallCount.ShouldBe(0);
    }

    private static AppDbContext CreateDb(string dbPath)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={dbPath}")
            .Options;
        var db = new AppDbContext(options);
        SchemaMigrator.ApplyAsync(db).GetAwaiter().GetResult();
        return db;
    }

    private sealed class CountingProvider : Interfaces.IMarketDataProvider
    {
        public int CallCount { get; private set; }

        public Task<IReadOnlyList<Candle>> GetMinuteBarsAsync(string ticker, DateOnly date, CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult<IReadOnlyList<Candle>>(TestCandles.CreateMinuteSeries(ticker, date, 3));
        }
    }
}
