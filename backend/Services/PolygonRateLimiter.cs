using Microsoft.Extensions.Options;
using TapeReplay.Api.Models;

namespace TapeReplay.Api.Services;

/// <summary>
/// Enforces a rolling per-minute call budget and mandatory cooldown after HTTP 429.
/// </summary>
public sealed class PolygonRateLimiter(
    IOptions<PolygonOptions> options,
    ILogger<PolygonRateLimiter> logger)
{
    private readonly PolygonOptions _options = options.Value;
    private readonly SemaphoreSlim _sync = new(1, 1);
    private readonly Queue<DateTime> _callTimestamps = new();
    private DateTime? _backoffUntilUtc;

    /// <summary>
    /// Waits until a call slot is available under the rolling window and any active backoff.
    /// </summary>
    public async Task WaitForSlotAsync(CancellationToken cancellationToken = default)
    {
        while (true)
        {
            await _sync.WaitAsync(cancellationToken);
            try
            {
                var now = DateTime.UtcNow;

                if (_backoffUntilUtc is { } backoffUntil && now < backoffUntil)
                {
                    var backoffDelay = backoffUntil - now;
                    logger.LogWarning(
                        "Polygon rate limit backoff active; waiting {Seconds:F0}s before next call.",
                        backoffDelay.TotalSeconds);
                    await Task.Delay(backoffDelay, cancellationToken);
                    continue;
                }

                PruneCallsOlderThanOneMinute(now);

                if (_callTimestamps.Count < _options.MaxCallsPerMinute)
                {
                    _callTimestamps.Enqueue(now);
                    logger.LogDebug(
                        "Polygon call slot granted ({Used}/{Max} in rolling 60s).",
                        _callTimestamps.Count,
                        _options.MaxCallsPerMinute);
                    return;
                }

                var oldest = _callTimestamps.Peek();
                var windowDelay = oldest.AddMinutes(1) - now;
                if (windowDelay > TimeSpan.Zero)
                {
                    logger.LogInformation(
                        "Polygon rolling limit reached ({Max}/min); waiting {Seconds:F0}s.",
                        _options.MaxCallsPerMinute,
                        windowDelay.TotalSeconds);
                    await Task.Delay(windowDelay, cancellationToken);
                    continue;
                }

                PruneCallsOlderThanOneMinute(DateTime.UtcNow);
            }
            finally
            {
                _sync.Release();
            }
        }
    }

    /// <summary>
    /// Starts the post-429 cooldown before any further Polygon calls.
    /// </summary>
    public async Task EnterBackoffAsync(CancellationToken cancellationToken = default)
    {
        await _sync.WaitAsync(cancellationToken);
        try
        {
            _backoffUntilUtc = DateTime.UtcNow.AddSeconds(_options.BackoffSecondsOn429);
            logger.LogWarning(
                "Polygon returned 429; backing off for {Seconds}s.",
                _options.BackoffSecondsOn429);
        }
        finally
        {
            _sync.Release();
        }
    }

    private void PruneCallsOlderThanOneMinute(DateTime now)
    {
        while (_callTimestamps.Count > 0 && now - _callTimestamps.Peek() >= TimeSpan.FromMinutes(1))
        {
            _callTimestamps.Dequeue();
        }
    }
}
