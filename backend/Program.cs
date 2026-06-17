using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Text.Json.Serialization;
using TapeReplay.Api.Data;
using TapeReplay.Api.Interfaces;
using TapeReplay.Api.Models.DataDistribution;
using TapeReplay.Api.Models;
using TapeReplay.Api.Services;
using TapeReplay.Api.Services.DataDistribution;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddJsonFile(
    $"appsettings.{builder.Environment.EnvironmentName}.local.json",
    optional: true,
    reloadOnChange: true);

builder.WebHost.UseUrls("http://localhost:5180");

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins("http://localhost:5173", "file://")
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

var dataDir = Environment.GetEnvironmentVariable("TAPEREPLAY_DATA_DIR");
var connectionString = ResolveSqliteConnectionString(builder.Configuration, builder.Environment.ContentRootPath, dataDir);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(connectionString));

builder.Services.Configure<DataDistributionOptions>(
    builder.Configuration.GetSection(DataDistributionOptions.SectionName));

builder.Services.Configure<RecordingJobOptions>(
    builder.Configuration.GetSection(RecordingJobOptions.SectionName));

builder.Services.AddHttpClient("DataCdn", client =>
{
    client.Timeout = TimeSpan.FromMinutes(10);
});

builder.Services.AddScoped<IMarketDataRepository, MarketDataRepository>();
builder.Services.AddScoped<ICoverageRepository, CoverageRepository>();
builder.Services.AddScoped<IMarketDailyRepository, MarketDailyRepository>();
builder.Services.AddScoped<IDataPartitionStateRepository, DataPartitionStateRepository>();

var apiKey = builder.Configuration["Polygon:ApiKey"];
var useMockData = builder.Configuration.GetValue<bool>("MarketData:UseMockProvider")
    || string.IsNullOrWhiteSpace(apiKey);

builder.Services.Configure<PolygonOptions>(builder.Configuration.GetSection(PolygonOptions.SectionName));
builder.Services.AddSingleton<PolygonRateLimiter>();

if (useMockData)
{
    builder.Services.AddSingleton<IMarketDataProvider, MockMarketDataProvider>();
}
else
{
    builder.Services.AddHttpClient<IMarketDataProvider, PolygonMarketDataProvider>();
}

builder.Services.AddSingleton<IStrategyParser, StrategyParser>();
builder.Services.AddSingleton<IStrategy, DailyHighBreakoutStrategy>();
builder.Services.AddSingleton<ITradeCostModel, TradeCostModel>();
builder.Services.AddSingleton<IHonestMetricsCalculator, HonestMetricsCalculator>();
builder.Services.AddSingleton<IBacktestEngine, BacktestEngine>();
builder.Services.AddScoped<IBacktestCommitRepository, BacktestCommitRepository>();
builder.Services.AddScoped<IBacktestHarness, BacktestHarness>();
builder.Services.AddScoped<PartitionImportService>();
builder.Services.AddScoped<DataPublisherService>();
builder.Services.AddScoped<DataSubscriberService>();
builder.Services.AddScoped<MarketDataScraperService>();
builder.Services.AddScoped<RecordingStartupService>();
builder.Services.AddScoped<MarketDataService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await SchemaMigrator.ApplyAsync(db);
}

var distributionOptions = app.Services.GetRequiredService<IOptions<DataDistributionOptions>>().Value;
if (distributionOptions.SyncOnLaunch && distributionOptions.CanSubscribe()
    && !string.IsNullOrWhiteSpace(distributionOptions.ManifestUrl))
{
    _ = Task.Run(async () =>
    {
        try
        {
            await using var scope = app.Services.CreateAsyncScope();
            var subscriber = scope.ServiceProvider.GetRequiredService<DataSubscriberService>();
            var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DataSync");
            var result = await subscriber.SyncAsync();
            logger.LogInformation(
                "Launch data sync: downloaded={Downloaded}, skipped={Skipped}, failed={Failed}",
                result.PartitionsDownloaded,
                result.PartitionsSkipped,
                result.PartitionsFailed);
        }
        catch (Exception ex)
        {
            var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("DataSync");
            logger.LogError(ex, "Launch data sync failed.");
        }
    });
}

if (distributionOptions.IsScraperEnabled())
{
    _ = Task.Run(async () =>
    {
        try
        {
            await using var scope = app.Services.CreateAsyncScope();
            var recording = scope.ServiceProvider.GetRequiredService<RecordingStartupService>();
            await recording.RunConfiguredJobsAsync();
        }
        catch (Exception ex)
        {
            var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("RecordingStartup");
            logger.LogError(ex, "Configured recording jobs failed.");
        }
    });
}

app.UseCors();
app.MapControllers();
app.MapGet("/api/health", () => Results.Ok(new { status = "ok" }));

app.Run();

static string ResolveSqliteConnectionString(IConfiguration configuration, string contentRootPath, string? dataDir)
{
    if (!string.IsNullOrWhiteSpace(dataDir))
    {
        Directory.CreateDirectory(dataDir);
        return $"Data Source={Path.Combine(dataDir, "tapereplay.db")}";
    }

    var configured = configuration.GetConnectionString("Default");
    if (!string.IsNullOrWhiteSpace(configured))
    {
        return ResolveRelativeSqlitePath(configured, contentRootPath);
    }

    return $"Data Source={Path.Combine(contentRootPath, "tapereplay.db")}";
}

static string ResolveRelativeSqlitePath(string connectionString, string contentRootPath)
{
    const string prefix = "Data Source=";
    if (!connectionString.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
    {
        return connectionString;
    }

    var path = connectionString[prefix.Length..].Trim();
    if (Path.IsPathRooted(path))
    {
        return connectionString;
    }

    return $"{prefix}{Path.Combine(contentRootPath, path)}";
}