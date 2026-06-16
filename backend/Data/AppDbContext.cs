using Microsoft.EntityFrameworkCore;
using TapeReplay.Api.Models;

namespace TapeReplay.Api.Data;

/// <summary>
/// EF Core database context. Provider is configured in DI (SQLite now, PostgreSQL later).
/// </summary>
public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<MarketDataEntity> MarketData => Set<MarketDataEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<MarketDataEntity>();

        entity.ToTable("MarketData");
        entity.HasKey(e => e.Id);
        entity.Property(e => e.Ticker).HasMaxLength(16).IsRequired();
        entity.Property(e => e.Open).HasPrecision(18, 6);
        entity.Property(e => e.High).HasPrecision(18, 6);
        entity.Property(e => e.Low).HasPrecision(18, 6);
        entity.Property(e => e.Close).HasPrecision(18, 6);
        entity.HasIndex(e => new { e.Ticker, e.DateTime });
    }
}
