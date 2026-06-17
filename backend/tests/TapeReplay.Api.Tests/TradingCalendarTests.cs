using Shouldly;
using TapeReplay.Api.Services.DataDistribution;

namespace TapeReplay.Api.Tests;

public sealed class TradingCalendarTests
{
    [Theory]
    [InlineData("2026-06-13")]
    [InlineData("2026-06-14")]
    public void Weekends_are_not_trading_days(string date)
    {
        var d = DateOnly.Parse(date);
        TradingCalendar.IsWeekend(d).ShouldBeTrue();
        TradingCalendar.IsTradingDay(d).ShouldBeFalse();
    }

    [Theory]
    [InlineData("2026-06-11")]
    [InlineData("2026-06-12")]
    [InlineData("2026-06-15")]
    public void Weekdays_are_trading_days(string date)
    {
        var d = DateOnly.Parse(date);
        TradingCalendar.IsWeekend(d).ShouldBeFalse();
        TradingCalendar.IsTradingDay(d).ShouldBeTrue();
    }
}
