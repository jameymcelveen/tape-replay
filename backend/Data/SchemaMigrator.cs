using Microsoft.EntityFrameworkCore;

namespace TapeReplay.Api.Data;

/// <summary>
/// Applies additive schema changes for existing SQLite databases created before new tables shipped.
/// </summary>
public static class SchemaMigrator
{
    public static async Task ApplyAsync(AppDbContext db, CancellationToken cancellationToken = default)
    {
        await db.Database.EnsureCreatedAsync(cancellationToken);

        await db.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS ticker_minute_coverage (
                Ticker TEXT NOT NULL,
                Date TEXT NOT NULL,
                Status TEXT NOT NULL,
                Provenance TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL,
                PRIMARY KEY (Ticker, Date)
            );
            """,
            cancellationToken);

        await db.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS market_daily_coverage (
                Date TEXT NOT NULL,
                Status TEXT NOT NULL,
                Provenance TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL,
                PRIMARY KEY (Date)
            );
            """,
            cancellationToken);

        await db.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS MarketDaily (
                Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                Ticker TEXT NOT NULL,
                Date TEXT NOT NULL,
                Open TEXT NOT NULL,
                High TEXT NOT NULL,
                Low TEXT NOT NULL,
                Close TEXT NOT NULL,
                Volume INTEGER NOT NULL
            );
            """,
            cancellationToken);

        await db.Database.ExecuteSqlRawAsync(
            """
            CREATE UNIQUE INDEX IF NOT EXISTS IX_MarketDaily_Ticker_Date ON MarketDaily (Ticker, Date);
            """,
            cancellationToken);

        await db.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS data_partition_imports (
                Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                Kind TEXT NOT NULL,
                PartitionKey TEXT NOT NULL,
                Sha256 TEXT NOT NULL,
                ImportedAt TEXT NOT NULL
            );
            """,
            cancellationToken);

        await db.Database.ExecuteSqlRawAsync(
            """
            CREATE UNIQUE INDEX IF NOT EXISTS IX_data_partition_imports_Kind_PartitionKey
                ON data_partition_imports (Kind, PartitionKey);
            """,
            cancellationToken);

        await db.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS data_publish_log (
                Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                Kind TEXT NOT NULL,
                PartitionKey TEXT NOT NULL,
                Sha256 TEXT NOT NULL,
                PublishedAt TEXT NOT NULL
            );
            """,
            cancellationToken);

        await db.Database.ExecuteSqlRawAsync(
            """
            CREATE UNIQUE INDEX IF NOT EXISTS IX_data_publish_log_Kind_PartitionKey
                ON data_publish_log (Kind, PartitionKey);
            """,
            cancellationToken);

        await db.Database.ExecuteSqlRawAsync(
            """
            CREATE UNIQUE INDEX IF NOT EXISTS IX_MarketData_Ticker_DateTime ON MarketData (Ticker, DateTime);
            """,
            cancellationToken);

        await db.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS strategy_results (
                Ticker TEXT NOT NULL,
                Date TEXT NOT NULL,
                StrategyConfigHash TEXT NOT NULL,
                HasData INTEGER NOT NULL,
                Traded INTEGER NOT NULL,
                PnlPct TEXT NULL,
                CapturePct TEXT NULL,
                PnlDollar TEXT NULL,
                EntryTime TEXT NULL,
                EntryPrice TEXT NULL,
                ExitTime TEXT NULL,
                ExitPrice TEXT NULL,
                ExitReason TEXT NULL,
                ComputedAt TEXT NOT NULL,
                PRIMARY KEY (Ticker, Date, StrategyConfigHash)
            );
            """,
            cancellationToken);
    }
}
