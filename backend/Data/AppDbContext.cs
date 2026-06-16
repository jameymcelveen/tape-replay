using Microsoft.EntityFrameworkCore;
using TapeReplay.Api.Models;

namespace TapeReplay.Api.Data;

/// <summary>
/// EF Core database context. Provider is configured in DI (SQLite now, PostgreSQL later).
/// </summary>
public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<MarketDataEntity> MarketData => Set<MarketDataEntity>();

    public DbSet<BacktestCommitEntity> BacktestCommits => Set<BacktestCommitEntity>();

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
        marketData.HasIndex(e => new { e.Ticker, e.DateTime });

        var commits = modelBuilder.Entity<BacktestCommitEntity>();
        commits.ToTable("BacktestCommits");
        commits.HasKey(e => e.Id);
        commits.Property(e => e.Ticker).HasMaxLength(16).IsRequired();
        commits.Property(e => e.StrategyJson).IsRequired();
        commits.Property(e => e.InSampleNetReturnPercent).HasPrecision(18, 6);
    }
}
