using Microsoft.Extensions.Options;
using Shouldly;
using TapeReplay.Api.Models;
using TapeReplay.Api.Services;

namespace TapeReplay.Api.Tests;

public sealed class PolygonRateLimiterTests
{
    [Fact]
    public async Task WaitForSlotAsync_enforces_max_calls_per_minute()
    {
        var limiter = new PolygonRateLimiter(
            Options.Create(new PolygonOptions { MaxCallsPerMinute = 2, BackoffSecondsOn429 = 1 }),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<PolygonRateLimiter>.Instance);

        var first = DateTime.UtcNow;
        await limiter.WaitForSlotAsync();
        await limiter.WaitForSlotAsync();

        var thirdStarted = DateTime.UtcNow;
        await limiter.WaitForSlotAsync();
        var elapsed = DateTime.UtcNow - thirdStarted;

        elapsed.ShouldBeGreaterThan(TimeSpan.FromMilliseconds(500));
    }

    [Fact]
    public async Task EnterBackoffAsync_delays_subsequent_slot()
    {
        var limiter = new PolygonRateLimiter(
            Options.Create(new PolygonOptions { MaxCallsPerMinute = 5, BackoffSecondsOn429 = 1 }),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<PolygonRateLimiter>.Instance);

        await limiter.EnterBackoffAsync();

        var started = DateTime.UtcNow;
        await limiter.WaitForSlotAsync();
        var elapsed = DateTime.UtcNow - started;

        elapsed.ShouldBeGreaterThan(TimeSpan.FromMilliseconds(900));
    }
}
