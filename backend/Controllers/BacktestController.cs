using Microsoft.AspNetCore.Mvc;
using TapeReplay.Api.Interfaces;
using TapeReplay.Api.Models;
using TapeReplay.Api.Models.ChartBacktest;
using TapeReplay.Api.Services;
using TapeReplay.Api.Services.ChartBacktest;

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
    ChartBacktestService chartBacktestService,
    StrategyHeatmapService strategyHeatmapService,
    ExploratoryGridService exploratoryGridService,
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
    /// Runs an intraday replay rule against stored minute bars and returns chart data with hindsight benchmark.
    /// </summary>
    [HttpPost("chart")]
    public async Task<ActionResult<ChartBacktestResponse>> Chart(
        [FromBody] ChartBacktestRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Ticker))
        {
            return BadRequest(new { error = "Ticker is required." });
        }

        if (request.To < request.From)
        {
            return BadRequest(new { error = "'to' must be on or after 'from'." });
        }

        if (request.Shares <= 0)
        {
            return BadRequest(new { error = "Shares must be positive." });
        }

        try
        {
            var response = await chartBacktestService.RunAsync(request, cancellationToken);
            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Computes strategy performance cells for a ticker-day heatmap grid with per-config caching.
    /// </summary>
    [HttpPost("chart/heatmap")]
    public async Task<ActionResult<StrategyHeatmapResponse>> ChartHeatmap(
        [FromBody] StrategyHeatmapRequest request,
        CancellationToken cancellationToken)
    {
        if (request.To < request.From)
        {
            return BadRequest(new { error = "'to' must be on or after 'from'." });
        }

        try
        {
            var response = await strategyHeatmapService.RunAsync(request, cancellationToken);
            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Exploratory net-after-costs grid across tickers and trading days. Not out-of-sample evidence.
    /// </summary>
    [HttpPost("exploratory-grid")]
    public async Task<ActionResult<ExploratoryGridResponse>> ExploratoryGrid(
        [FromBody] ExploratoryGridRequest request,
        CancellationToken cancellationToken)
    {
        if (request.To < request.From)
        {
            return BadRequest(new { error = "'to' must be on or after 'from'." });
        }

        if (request.Tickers.Count == 0)
        {
            return BadRequest(new { error = "At least one ticker is required." });
        }

        try
        {
            var response = await exploratoryGridService.RunAsync(request, cancellationToken);
            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
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
