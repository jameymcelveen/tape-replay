using System.Globalization;
using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Options;
using TapeReplay.Api.Interfaces;
using TapeReplay.Api.Models;

namespace TapeReplay.Api.Services;

/// <summary>
/// Polygon.io market data provider implementation.
/// </summary>
public sealed class PolygonMarketDataProvider(
    HttpClient httpClient,
    IOptions<PolygonOptions> options,
    PolygonRateLimiter rateLimiter,
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
        var polygon = options.Value;
        if (string.IsNullOrWhiteSpace(polygon.ApiKey))
        {
            logger.LogWarning("Polygon API key not configured");
            return [];
        }

        var normalizedTicker = ticker.ToUpperInvariant();
        var url = string.Create(
            CultureInfo.InvariantCulture,
            $"https://api.polygon.io/v2/aggs/ticker/{normalizedTicker}/range/1/minute/{date:yyyy-MM-dd}/{date:yyyy-MM-dd}?adjusted=true&sort=asc&limit=50000&apiKey={polygon.ApiKey}");

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await rateLimiter.WaitForSlotAsync(cancellationToken);

            using var response = await httpClient.GetAsync(url, cancellationToken);
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                logger.LogError("Polygon rate limited (429): {Body}", body);
                await rateLimiter.EnterBackoffAsync(cancellationToken);
                continue;
            }

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
