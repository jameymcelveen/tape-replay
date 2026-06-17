using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Text.Json.Serialization;
using TapeReplay.Api.Data;
using TapeReplay.Api.Interfaces;
using TapeReplay.Api.Models.DataDistribution;
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
if (!string.IsNullOrWhiteSpace(dataDir))
{
    Directory.CreateDirectory(dataDir);
}

var dbFileName = string.IsNullOrWhiteSpace(dataDir)
    ? "tapereplay.db"
    : Path.Combine(dataDir, "tapereplay.db");

var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? $"Data Source={dbFileName}";

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(connectionString));

builder.Services.Configure<DataDistributionOptions>(
    builder.Configuration.GetSection(DataDistributionOptions.SectionName));

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

app.UseCors();
app.MapControllers();
app.MapGet("/api/health", () => Results.Ok(new { status = "ok" }));

app.Run();