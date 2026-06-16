using Microsoft.EntityFrameworkCore;
using System.Text.Json.Serialization;
using TapeReplay.Api.Data;
using TapeReplay.Api.Interfaces;
using TapeReplay.Api.Services;

var builder = WebApplication.CreateBuilder(args);

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

builder.Services.AddScoped<IMarketDataRepository, MarketDataRepository>();

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
builder.Services.AddSingleton<IBacktestEngine, BacktestEngine>();
builder.Services.AddScoped<MarketDataService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
}

app.UseCors();
app.MapControllers();
app.MapGet("/api/health", () => Results.Ok(new { status = "ok" }));

app.Run();
