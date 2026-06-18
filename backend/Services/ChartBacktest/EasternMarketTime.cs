namespace TapeReplay.Api.Services.ChartBacktest;

/// <summary>
/// Converts Eastern market session wall-clock times to UTC.
/// </summary>
public static class EasternMarketTime
{
    private static readonly TimeZoneInfo Eastern = MarketSessionClassifier.ResolveEasternTimeZoneForConversion();

    /// <summary>Extended session open (04:00 ET).</summary>
    public static readonly TimeOnly ExtendedOpen = new(4, 0);

    /// <summary>Extended session close (20:00 ET).</summary>
    public static readonly TimeOnly ExtendedClose = new(20, 0);

    /// <summary>Regular session open (09:30 ET).</summary>
    public static readonly TimeOnly RegularOpen = new(9, 30);

    /// <summary>Regular session close (16:00 ET).</summary>
    public static readonly TimeOnly RegularClose = new(16, 0);

    /// <summary>
    /// Converts an Eastern date and time to UTC.
    /// </summary>
    public static DateTime ToUtc(DateOnly date, TimeOnly time)
    {
        var local = DateTime.SpecifyKind(date.ToDateTime(time), DateTimeKind.Unspecified);
        return TimeZoneInfo.ConvertTimeToUtc(local, Eastern);
    }

    /// <summary>
    /// UTC bounds for loading stored bars across an Eastern date range.
    /// </summary>
    public static (DateTime FromUtc, DateTime ToUtc) ExtendedSessionBounds(DateOnly from, DateOnly to) =>
        (ToUtc(from, ExtendedOpen), ToUtc(to, ExtendedClose));
}
