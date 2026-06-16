using Shouldly;
using TapeReplay.Api.Interfaces;
using TapeReplay.Api.Models;
using TapeReplay.Api.Services;
using TapeReplay.Api.Tests.Helpers;

namespace TapeReplay.Api.Tests;

public sealed class HonestMetricsCalculatorTests
{
    private readonly HonestMetricsCalculator _sut = new();

    [Fact]
    public void Max_drawdown_is_computed_from_equity_curve()
    {
        var trades = new List<TradeResult>
        {
            new() { GrossPnL = 100, NetPnL = 80, TotalCosts = 20, Quantity = 1, ExitReason = "tp" },
            new() { GrossPnL = -200, NetPnL = -220, TotalCosts = 20, Quantity = 1, ExitReason = "sl" }
        };

        var curve = new List<EquityPoint>
        {
            new() { Date = new DateOnly(2024, 1, 1), Equity = 25_000m },
            new() { Date = new DateOnly(2024, 1, 2), Equity = 25_080m },
            new() { Date = new DateOnly(2024, 1, 3), Equity = 24_860m }
        };

        var metrics = _sut.Compute(trades, curve, 25_000m, SampleLabel.OutOfSample);

        metrics.MaxDrawdownAbsolute.ShouldBe(220m);
        metrics.MaxDrawdownPercent.ShouldBeGreaterThan(0);
        metrics.Verdict.ShouldContain("Out-of-sample");
    }

    [Fact]
    public void Verdict_is_not_encouraging_when_drawdown_is_severe()
    {
        var trades = Enumerable.Range(0, 5).Select(_ => new TradeResult
        {
            GrossPnL = -500,
            NetPnL = -520,
            TotalCosts = 20,
            Quantity = 10,
            ExitReason = "stop_loss"
        }).ToList();

        var curve = new List<EquityPoint>
        {
            new() { Date = new DateOnly(2024, 1, 1), Equity = 25_000m },
            new() { Date = new DateOnly(2024, 1, 5), Equity = 22_400m }
        };

        var metrics = _sut.Compute(trades, curve, 25_000m, SampleLabel.OutOfSample);
        metrics.Verdict.ShouldContain("would likely not survive");
    }
}
