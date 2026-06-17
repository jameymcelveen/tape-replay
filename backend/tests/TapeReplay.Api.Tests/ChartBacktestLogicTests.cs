using Shouldly;
using TapeReplay.Api.Models.ChartBacktest;
using TapeReplay.Api.Services.ChartBacktest;
using TapeReplay.Api.Services.ChartBacktest.Rules;

namespace TapeReplay.Api.Tests;

public sealed class ChartBacktestLogicTests
{
    [Fact]
    public void Classify_RegularSessionAt0930Eastern_IsRegular()
    {
        // 2026-06-16 09:30 ET (EDT) = 13:30 UTC
        var utc = new DateTime(2026, 6, 16, 13, 30, 0, DateTimeKind.Utc);
        MarketSessionClassifier.Classify(utc).ShouldBe(MarketSession.Regular);
    }

    [Fact]
    public void Classify_PremarketBefore0930Eastern_IsPremarket()
    {
        var utc = new DateTime(2026, 6, 16, 12, 0, 0, DateTimeKind.Utc);
        MarketSessionClassifier.Classify(utc).ShouldBe(MarketSession.Premarket);
    }

    [Fact]
    public void Hindsight_FindsBestLongPair()
    {
        var bars = new List<EnrichedBar>
        {
            Bar(0, low: 10m, high: 11m),
            Bar(1, low: 9m, high: 12m),
            Bar(2, low: 8m, high: 15m),
            Bar(3, low: 14m, high: 16m)
        };

        var result = PerfectHindsightCalculator.Compute(bars);

        result.BuyPrice.ShouldBe(8m);
        result.SellPrice.ShouldBe(16m);
        result.ProfPerShare.ShouldBe(8m);
    }

    [Fact]
    public void Orb_BreakoutThenStop_FillsStopFirstOnSameBar()
    {
        var day = BuildOrbDay(
            openingHigh: 10m,
            breakoutHigh: 11m,
            breakoutLow: 8.5m,
            stopPct: 5m,
            targetPct: 20m);

        var strategy = new OrbReplayStrategy();
        var result = strategy.EvaluateDay(day, new ReplayRuleParams { OrMinutes = 2, StopPct = 5, TargetPct = 20 }, 100);

        result.Taken.ShouldBeTrue();
        result.EntryPrice.ShouldBe(10m);
        result.ExitReason.ShouldBe("stop");
        result.ExitPrice.ShouldBe(9.5m);
    }

    [Fact]
    public void Pmh_RequiresPremarketHighBreak()
    {
        var day = new List<EnrichedBar>
        {
            BarAtEt(2026, 6, 16, 8, 0, low: 2m, high: 2.5m, session: MarketSession.Premarket),
            BarAtEt(2026, 6, 16, 9, 30, low: 2.4m, high: 2.6m, session: MarketSession.Regular),
            BarAtEt(2026, 6, 16, 9, 31, low: 2.55m, high: 2.8m, session: MarketSession.Regular),
            BarAtEt(2026, 6, 16, 9, 32, low: 2.7m, high: 2.9m, session: MarketSession.Regular)
        };

        var strategy = new PmhReplayStrategy();
        var result = strategy.EvaluateDay(day, new ReplayRuleParams { StopPct = 50, TargetPct = 50 }, 100);

        result.Taken.ShouldBeTrue();
        result.EntryPrice.ShouldBe(2.5m);
    }

    private static List<EnrichedBar> BuildOrbDay(
        decimal openingHigh,
        decimal breakoutHigh,
        decimal breakoutLow,
        decimal stopPct,
        decimal targetPct)
    {
        _ = stopPct;
        _ = targetPct;

        return
        [
            BarAtEt(2026, 6, 16, 9, 30, low: openingHigh - 0.5m, high: openingHigh, session: MarketSession.Regular),
            BarAtEt(2026, 6, 16, 9, 31, low: openingHigh - 0.2m, high: openingHigh - 0.1m, session: MarketSession.Regular),
            BarAtEt(2026, 6, 16, 9, 32, low: breakoutLow, high: breakoutHigh, session: MarketSession.Regular),
            BarAtEt(2026, 6, 16, 9, 33, low: breakoutHigh, high: breakoutHigh + 0.1m, session: MarketSession.Regular)
        ];
    }

    private static EnrichedBar Bar(int minuteOffset, decimal low, decimal high) => new()
    {
        UtcTime = new DateTime(2026, 6, 16, 13, 30, 0, DateTimeKind.Utc).AddMinutes(minuteOffset),
        Open = low,
        Low = low,
        High = high,
        Close = high,
        Volume = 1000,
        Session = MarketSession.Regular,
        EasternDate = new DateOnly(2026, 6, 16)
    };

    private static EnrichedBar BarAtEt(
        int year,
        int month,
        int day,
        int hour,
        int minute,
        decimal low,
        decimal high,
        MarketSession session)
    {
        var et = new DateTime(year, month, day, hour, minute, 0, DateTimeKind.Unspecified);
        var utc = TimeZoneInfo.ConvertTimeToUtc(et, TimeZoneInfo.FindSystemTimeZoneById(
            OperatingSystem.IsWindows() ? "Eastern Standard Time" : "America/New_York"));

        return new EnrichedBar
        {
            UtcTime = utc,
            Open = low,
            Low = low,
            High = high,
            Close = high,
            Volume = 1000,
            Session = session,
            EasternDate = new DateOnly(year, month, day)
        };
    }
}
