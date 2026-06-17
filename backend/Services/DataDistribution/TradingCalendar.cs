namespace TapeReplay.Api.Services.DataDistribution;

/// <summary>
/// US equity session calendar helpers (weekends only for MVP).
/// </summary>
public static class TradingCalendar
{
    public static bool IsWeekend(DateOnly date) =>
        date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;

    public static bool IsTradingDay(DateOnly date) => !IsWeekend(date);
}
