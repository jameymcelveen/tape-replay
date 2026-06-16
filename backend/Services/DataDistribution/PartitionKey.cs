using TapeReplay.Api.Models.DataDistribution;

namespace TapeReplay.Api.Services.DataDistribution;

/// <summary>
/// Partition key helpers for minute (ticker + year-month) and daily (year-month) layouts.
/// </summary>
public static class PartitionKey
{
    public static string Minute(string ticker, int year, int month) =>
        $"{ticker.ToUpperInvariant()}_{year:D4}_{month:D2}";

    public static string Daily(int year, int month) => $"{year:D4}_{month:D2}";

    public static (string Ticker, int Year, int Month) ParseMinute(string key)
    {
        var parts = key.Split('_', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 3)
        {
            throw new FormatException($"Invalid minute partition key: {key}");
        }

        return (parts[0], int.Parse(parts[1]), int.Parse(parts[2]));
    }

    public static (int Year, int Month) ParseDaily(string key)
    {
        var parts = key.Split('_', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2)
        {
            throw new FormatException($"Invalid daily partition key: {key}");
        }

        return (int.Parse(parts[0]), int.Parse(parts[1]));
    }

    public static string KindToManifestValue(PartitionKind kind) =>
        kind == PartitionKind.Minute ? "minute" : "daily";

    public static PartitionKind KindFromManifestValue(string value) =>
        value.Equals("minute", StringComparison.OrdinalIgnoreCase) ? PartitionKind.Minute : PartitionKind.Daily;
}
