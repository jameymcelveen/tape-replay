using Microsoft.AspNetCore.Mvc;
using TapeReplay.Api.Interfaces;
using TapeReplay.Api.Models;
using TapeReplay.Api.Services;

namespace TapeReplay.Api.Controllers;

/// <summary>
/// Backtest execution endpoints.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public sealed class BacktestController(
    IBacktestEngine backtestEngine,
    MarketDataService marketDataService,
    IStrategyParser parser) : ControllerBase
{
    /// <summary>
    /// Runs a single-day backtest for the given ticker, date, and strategy.
    /// </summary>
    [HttpPost("run")]
    public async Task<ActionResult<BacktestResult>> Run(
        [FromBody] BacktestRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Ticker))
        {
            return BadRequest(new { error = "Ticker is required." });
        }

        StrategyConfig config;
        try
        {
            config = request.Strategy ?? parser.Parse(request.Dsl ?? string.Empty);
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }

        var bars = await marketDataService.GetMinuteBarsAsync(request.Ticker, request.Date, cancellationToken);
        var result = backtestEngine.Run(request.Ticker, request.Date, config, bars);
        return Ok(result);
    }
}
