using System.Globalization;
using System.Text.Json;
using TapeReplay.Api.Interfaces;
using TapeReplay.Api.Models;

namespace TapeReplay.Api.Services;

/// <summary>
/// Polygon.io market data provider implementation.
/// </summary>
public sealed class PolygonMarketDataProvider(
    HttpClient httpClient,
    IConfiguration configuration,
    ILogger<PolygonMarketDataProvider> logger) : IMarketDataProvider
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<IReadOnlyList<Candle>> GetMinuteBarsAsync(
        string ticker,
        DateOnly date,
        CancellationToken cancellationToken = default)
    {
        var apiKey = configuration["Polygon:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            logger.LogWarning("Polygon API key not configured");
            return [];
        }

        var normalizedTicker = ticker.ToUpperInvariant();
        var url = string.Create(
            CultureInfo.InvariantCulture,
            $"https://api.polygon.io/v2/aggs/ticker/{normalizedTicker}/range/1/minute/{date:yyyy-MM-dd}/{date:yyyy-MM-dd}?adjusted=true&sort=asc&limit=50000&apiKey={apiKey}");

        using var response = await httpClient.GetAsync(url, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            logger.LogError("Polygon request failed: {Status} {Body}", response.StatusCode, body);
            return [];
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var payload = await JsonSerializer.DeserializeAsync<PolygonAggregateResponse>(stream, JsonOptions, cancellationToken);

        if (payload?.Results is null || payload.Results.Count == 0)
        {
            return [];
        }

        return payload.Results
            .Select(result => new Candle
            {
                Ticker = normalizedTicker,
                DateTime = DateTimeOffset.FromUnixTimeMilliseconds(result.T).UtcDateTime,
                Open = result.O,
                High = result.H,
                Low = result.L,
                Close = result.C,
                Volume = (long)result.V
            })
            .OrderBy(c => c.DateTime)
            .ToList();
    }

    private sealed class PolygonAggregateResponse
    {
        public List<PolygonBar>? Results { get; init; }
    }

    private sealed class PolygonBar
    {
        public long T { get; init; }

        public decimal O { get; init; }

        public decimal H { get; init; }

        public decimal L { get; init; }

        public decimal C { get; init; }

        public double V { get; init; }
    }
}
