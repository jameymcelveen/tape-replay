using TapeReplay.Api.Models.ChartBacktest;

namespace TapeReplay.Api.Services.ChartBacktest;

/// <summary>
/// Classifies UTC bar timestamps into US equity session buckets using Eastern Time.
/// </summary>
public static class MarketSessionClassifier
{
    private static readonly TimeZoneInfo Eastern = ResolveEasternTimeZone();

    private static readonly TimeOnly RegularOpen = new(9, 30);
    private static readonly TimeOnly RegularClose = new(16, 0);
    private static readonly TimeOnly PostClose = new(20, 0);

    /// <summary>
    /// Converts a UTC bar timestamp to US Eastern local time.
    /// </summary>
    public static DateTime ToEastern(DateTime utc)
    {
        var normalized = utc.Kind switch
        {
            DateTimeKind.Utc => utc,
            DateTimeKind.Local => utc.ToUniversalTime(),
            _ => DateTime.SpecifyKind(utc, DateTimeKind.Utc)
        };

        return TimeZoneInfo.ConvertTimeFromUtc(normalized, Eastern);
    }

    /// <summary>
    /// Classifies a bar into premarket, regular, or post-market session.
    /// </summary>
    public static MarketSession Classify(DateTime utc)
    {
        var et = ToEastern(utc);
        var time = TimeOnly.FromDateTime(et);

        if (time >= RegularOpen && time < RegularClose)
        {
            return MarketSession.Regular;
        }

        if (time < RegularOpen)
        {
            return MarketSession.Premarket;
        }

        return time < PostClose ? MarketSession.Post : MarketSession.Post;
    }

    /// <summary>
    /// Whether a bar belongs in the requested scope filter.
    /// </summary>
    public static bool IsInScope(MarketSession session, DateTime utc, bool includeExtendedHours)
    {
        if (!includeExtendedHours)
        {
            return session == MarketSession.Regular;
        }

        var time = TimeOnly.FromDateTime(ToEastern(utc));
        return time < PostClose;
    }

    /// <summary>
    /// Returns the Eastern calendar date for a UTC bar timestamp.
    /// </summary>
    public static DateOnly GetEasternDate(DateTime utc) => DateOnly.FromDateTime(ToEastern(utc));

    /// <summary>
    /// Resolves the US Eastern time zone for conversions.
    /// </summary>
    public static TimeZoneInfo ResolveEasternTimeZoneForConversion() => ResolveEasternTimeZone();

    private static TimeZoneInfo ResolveEasternTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("America/New_York");
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
        }
    }
}
