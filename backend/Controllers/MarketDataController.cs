using Microsoft.AspNetCore.Mvc;
using TapeReplay.Api.Services;

namespace TapeReplay.Api.Controllers;

/// <summary>
/// Market data cache and fetch status endpoints.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public sealed class MarketDataController(MarketDataService marketDataService) : ControllerBase
{
    /// <summary>
    /// Returns minute bars for a ticker and date, fetching from the provider when cache is empty.
    /// </summary>
    [HttpGet("{ticker}/{date}")]
    public async Task<IActionResult> GetBars(string ticker, DateOnly date, CancellationToken cancellationToken)
    {
        var bars = await marketDataService.GetMinuteBarsAsync(ticker, date, cancellationToken);
        return Ok(new
        {
            ticker = ticker.ToUpperInvariant(),
            date,
            barCount = bars.Count,
            bars
        });
    }
}
