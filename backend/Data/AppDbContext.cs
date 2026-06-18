using Microsoft.EntityFrameworkCore;
using TapeReplay.Api.Models;
using TapeReplay.Api.Models.ChartBacktest;
using TapeReplay.Api.Models.DataDistribution;

namespace TapeReplay.Api.Data;

/// <summary>
/// EF Core database context. Provider is configured in DI (SQLite now, PostgreSQL later).
/// </summary>
public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<MarketDataEntity> MarketData => Set<MarketDataEntity>();

    public DbSet<BacktestCommitEntity> BacktestCommits => Set<BacktestCommitEntity>();

    public DbSet<TickerMinuteCoverageEntity> TickerMinuteCoverage => Set<TickerMinuteCoverageEntity>();

    public DbSet<MarketDailyCoverageEntity> MarketDailyCoverage => Set<MarketDailyCoverageEntity>();

    public DbSet<MarketDailyEntity> MarketDaily => Set<MarketDailyEntity>();

    public DbSet<DataPartitionImportEntity> DataPartitionImports => Set<DataPartitionImportEntity>();

    public DbSet<DataPublishLogEntity> DataPublishLogs => Set<DataPublishLogEntity>();

    public DbSet<StrategyResultEntity> StrategyResults => Set<StrategyResultEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var marketData = modelBuilder.Entity<MarketDataEntity>();

        marketData.ToTable("MarketData");
        marketData.HasKey(e => e.Id);
        marketData.Property(e => e.Ticker).HasMaxLength(16).IsRequired();
        marketData.Property(e => e.Open).HasPrecision(18, 6);
        marketData.Property(e => e.High).HasPrecision(18, 6);
        marketData.Property(e => e.Low).HasPrecision(18, 6);
        marketData.Property(e => e.Close).HasPrecision(18, 6);
        marketData.HasIndex(e => new { e.Ticker, e.DateTime }).IsUnique();

        var commits = modelBuilder.Entity<BacktestCommitEntity>();
        commits.ToTable("BacktestCommits");
        commits.HasKey(e => e.Id);
        commits.Property(e => e.Ticker).HasMaxLength(16).IsRequired();
        commits.Property(e => e.StrategyJson).IsRequired();
        commits.Property(e => e.InSampleNetReturnPercent).HasPrecision(18, 6);

        var minuteCoverage = modelBuilder.Entity<TickerMinuteCoverageEntity>();
        minuteCoverage.ToTable("ticker_minute_coverage");
        minuteCoverage.HasKey(e => new { e.Ticker, e.Date });
        minuteCoverage.Property(e => e.Ticker).HasMaxLength(16);
        minuteCoverage.Property(e => e.Status).HasConversion<string>().HasMaxLength(16);
        minuteCoverage.Property(e => e.Provenance).HasConversion<string>().HasMaxLength(16);

        var dailyCoverage = modelBuilder.Entity<MarketDailyCoverageEntity>();
        dailyCoverage.ToTable("market_daily_coverage");
        dailyCoverage.HasKey(e => e.Date);
        dailyCoverage.Property(e => e.Status).HasConversion<string>().HasMaxLength(16);
        dailyCoverage.Property(e => e.Provenance).HasConversion<string>().HasMaxLength(16);

        var daily = modelBuilder.Entity<MarketDailyEntity>();
        daily.ToTable("MarketDaily");
        daily.HasKey(e => e.Id);
        daily.Property(e => e.Ticker).HasMaxLength(16).IsRequired();
        daily.Property(e => e.Open).HasPrecision(18, 6);
        daily.Property(e => e.High).HasPrecision(18, 6);
        daily.Property(e => e.Low).HasPrecision(18, 6);
        daily.Property(e => e.Close).HasPrecision(18, 6);
        daily.HasIndex(e => new { e.Ticker, e.Date }).IsUnique();

        var imports = modelBuilder.Entity<DataPartitionImportEntity>();
        imports.ToTable("data_partition_imports");
        imports.HasKey(e => e.Id);
        imports.Property(e => e.PartitionKey).HasMaxLength(64).IsRequired();
        imports.Property(e => e.Sha256).HasMaxLength(64).IsRequired();
        imports.Property(e => e.Kind).HasConversion<string>().HasMaxLength(16);
        imports.HasIndex(e => new { e.Kind, e.PartitionKey }).IsUnique();

        var publishLog = modelBuilder.Entity<DataPublishLogEntity>();
        publishLog.ToTable("data_publish_log");
        publishLog.HasKey(e => e.Id);
        publishLog.Property(e => e.PartitionKey).HasMaxLength(64).IsRequired();
        publishLog.Property(e => e.Sha256).HasMaxLength(64).IsRequired();
        publishLog.Property(e => e.Kind).HasConversion<string>().HasMaxLength(16);
        publishLog.HasIndex(e => new { e.Kind, e.PartitionKey }).IsUnique();

        var strategyResults = modelBuilder.Entity<StrategyResultEntity>();
        strategyResults.ToTable("strategy_results");
        strategyResults.HasKey(e => new { e.Ticker, e.Date, e.StrategyConfigHash });
        strategyResults.Property(e => e.Ticker).HasMaxLength(16);
        strategyResults.Property(e => e.StrategyConfigHash).HasMaxLength(64);
        strategyResults.Property(e => e.PnlPct).HasPrecision(18, 6);
        strategyResults.Property(e => e.CapturePct).HasPrecision(18, 6);
        strategyResults.Property(e => e.PnlDollar).HasPrecision(18, 6);
        strategyResults.Property(e => e.EntryPrice).HasPrecision(18, 6);
        strategyResults.Property(e => e.ExitPrice).HasPrecision(18, 6);
    }
}
