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
    IBacktestHarness backtestHarness,
    MarketDataService marketDataService,
    IStrategyParser parser) : ControllerBase
{
    /// <summary>
    /// Exploratory single-day backtest. Not evidence of edge.
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

        var costs = TradeCostDefaults.Resolve(request.Costs);
        var bars = await marketDataService.GetMinuteBarsAsync(request.Ticker, request.Date, cancellationToken);
        var result = backtestEngine.Run(
            request.Ticker,
            request.Date,
            config,
            bars,
            costs,
            SampleLabel.Exploratory,
            request.StartingCapitalUsd);

        return Ok(result);
    }

    /// <summary>
    /// Freeze strategy config against an in-sample date range.
    /// </summary>
    [HttpPost("commit")]
    public async Task<ActionResult<BacktestCommitResponse>> Commit(
        [FromBody] BacktestCommitRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Ticker))
        {
            return BadRequest(new { error = "Ticker is required." });
        }

        try
        {
            var response = await backtestHarness.CommitAsync(request, cancellationToken);
            return Ok(response);
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Score a frozen strategy on an out-of-sample date range.
    /// </summary>
    [HttpPost("evaluate")]
    public async Task<ActionResult<BacktestEvaluateResponse>> Evaluate(
        [FromBody] BacktestEvaluateRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await backtestHarness.EvaluateAsync(request, cancellationToken);
            return Ok(response);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
