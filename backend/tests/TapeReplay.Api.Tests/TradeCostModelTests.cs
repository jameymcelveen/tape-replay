using Shouldly;
using TapeReplay.Api.Models;
using TapeReplay.Api.Services;

namespace TapeReplay.Api.Tests;

public sealed class TradeCostModelTests
{
    private readonly TradeCostModel _sut = new();

    [Fact]
    public void Default_costs_are_never_zero()
    {
        var config = new TradeCostConfig();
        var entry = _sut.CalculateEntry(100m, 10, config);
        var exit = _sut.CalculateExit(100m, 10, config);

        entry.TotalCost.ShouldBeGreaterThan(0);
        exit.TotalCost.ShouldBeGreaterThan(0);
        entry.FillPrice.ShouldBeGreaterThan(100m);
        exit.FillPrice.ShouldBeLessThan(100m);
    }

    [Fact]
    public void Net_pnl_after_costs_is_less_than_gross()
    {
        var config = new TradeCostConfig();
        var entry = _sut.CalculateEntry(100m, 100, config);
        var exit = _sut.CalculateExit(105m, 100, config);
        var gross = (105m - 100m) * 100;
        var net = gross - entry.TotalCost - exit.TotalCost;

        net.ShouldBeLessThan(gross);
    }
}
