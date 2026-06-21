using System.Globalization;
using System.Text;
using TapeReplay.Api.Interfaces;
using TapeReplay.Api.Models;

namespace TapeReplay.Api.Services;

/// <summary>
/// Parses and generates the MVP strategy DSL format.
/// </summary>
public sealed class StrategyParser : IStrategyParser
{
    public StrategyConfig Parse(string dsl)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dsl);

        var config = new StrategyConfig();
        var takeProfits = new List<TakeProfitTarget>();
        var section = string.Empty;

        foreach (var rawLine in dsl.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var line = rawLine.Trim();
            if (line.Length == 0)
            {
                continue;
            }

            if (line.StartsWith("strategy ", StringComparison.OrdinalIgnoreCase))
            {
                config.Name = ExtractQuotedValue(line["strategy ".Length..]);
                continue;
            }

            if (line.Equals("entry_rules {", StringComparison.OrdinalIgnoreCase))
            {
                section = "entry";
                continue;
            }

            if (line.Equals("exit_rules {", StringComparison.OrdinalIgnoreCase))
            {
                section = "exit";
                continue;
            }

            if (line.Equals("risk_management {", StringComparison.OrdinalIgnoreCase))
            {
                section = "risk";
                continue;
            }

            if (line == "}")
            {
                section = string.Empty;
                continue;
            }

            if (line.Equals("on price_breaks_above_daily_high", StringComparison.OrdinalIgnoreCase))
            {
                config.EntryTrigger = EntryTriggerType.PriceBreaksAboveDailyHigh;
                continue;
            }

            if (line.Equals("on opening_range_high_break", StringComparison.OrdinalIgnoreCase))
            {
                config.EntryTrigger = EntryTriggerType.OpeningRangeHighBreak;
                continue;
            }

            if (line.StartsWith("set position_size to ", StringComparison.OrdinalIgnoreCase))
            {
                var value = line["set position_size to ".Length..].Replace(" USD", string.Empty, StringComparison.OrdinalIgnoreCase).Trim();
                config.PositionSizeUsd = decimal.Parse(value, CultureInfo.InvariantCulture);
                continue;
            }

            if (section == "entry")
            {
                ParseEntryRule(line, config);
                continue;
            }

            if (section == "exit")
            {
                ParseExitRule(line, config, takeProfits);
                continue;
            }

            if (section == "risk")
            {
                ParseRiskRule(line, config);
            }
        }

        config.TakeProfitTargets = takeProfits;
        return config;
    }

    public string Generate(StrategyConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        var builder = new StringBuilder();
        builder.AppendLine(CultureInfo.InvariantCulture, $"strategy \"{config.Name}\"");
        builder.AppendLine();
        builder.AppendLine(config.EntryTrigger == EntryTriggerType.OpeningRangeHighBreak
            ? "on opening_range_high_break"
            : "on price_breaks_above_daily_high");
        builder.AppendLine();
        builder.AppendLine(CultureInfo.InvariantCulture, $"set position_size to {config.PositionSizeUsd:0} USD");
        builder.AppendLine();
        builder.AppendLine("entry_rules {");
        builder.AppendLine(CultureInfo.InvariantCulture, $"  opening_range_minutes {config.OpeningRangeMinutes}");
        builder.AppendLine(CultureInfo.InvariantCulture, $"  entry_window {config.EntryWindowStart:HH:mm} to {config.EntryWindowEnd:HH:mm}");
        builder.AppendLine(CultureInfo.InvariantCulture, $"  first_breakout_only {config.FirstBreakoutOnly.ToString().ToLowerInvariant()}");
        builder.AppendLine(CultureInfo.InvariantCulture, $"  regular_session_only {config.RegularSessionOnly.ToString().ToLowerInvariant()}");
        builder.AppendLine("}");
        builder.AppendLine();
        builder.AppendLine("exit_rules {");

        builder.AppendLine(CultureInfo.InvariantCulture, $"  stop_loss {config.StopLossPercent:0.##}%");

        for (var i = 0; i < config.TakeProfitTargets.Count; i++)
        {
            var target = config.TakeProfitTargets[i];
            builder.AppendLine(
                CultureInfo.InvariantCulture,
                $"  take_profit_{i + 1} {target.Percent:0.##}% at {target.Weight * 100:0}% weight");
        }

        builder.AppendLine(CultureInfo.InvariantCulture, $"  close_all_at {config.CloseAllAt:HH:mm}");
        builder.AppendLine("}");
        builder.AppendLine();
        builder.AppendLine("risk_management {");
        builder.AppendLine(CultureInfo.InvariantCulture, $"  max_daily_loss {config.MaxDailyLossUsd:0} USD");
        builder.AppendLine(CultureInfo.InvariantCulture, $"  max_concurrent_trades {config.MaxConcurrentTrades}");
        builder.AppendLine(CultureInfo.InvariantCulture, $"  max_trades_per_day {config.MaxTradesPerDay}");
        builder.AppendLine(CultureInfo.InvariantCulture, $"  no_reentry_after_stop {config.NoReentryAfterStop.ToString().ToLowerInvariant()}");
        builder.AppendLine("}");

        return builder.ToString().TrimEnd();
    }

    private static void ParseEntryRule(string line, StrategyConfig config)
    {
        if (line.StartsWith("opening_range_minutes ", StringComparison.OrdinalIgnoreCase))
        {
            config.OpeningRangeMinutes = int.Parse(line["opening_range_minutes ".Length..].Trim(), CultureInfo.InvariantCulture);
            return;
        }

        if (line.StartsWith("entry_window ", StringComparison.OrdinalIgnoreCase))
        {
            var window = line["entry_window ".Length..].Split(" to ", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (window.Length == 2)
            {
                config.EntryWindowStart = TimeOnly.ParseExact(window[0], "H:mm", CultureInfo.InvariantCulture);
                config.EntryWindowEnd = TimeOnly.ParseExact(window[1], "H:mm", CultureInfo.InvariantCulture);
            }

            return;
        }

        if (line.StartsWith("first_breakout_only ", StringComparison.OrdinalIgnoreCase))
        {
            config.FirstBreakoutOnly = bool.Parse(line["first_breakout_only ".Length..].Trim());
            return;
        }

        if (line.StartsWith("regular_session_only ", StringComparison.OrdinalIgnoreCase))
        {
            config.RegularSessionOnly = bool.Parse(line["regular_session_only ".Length..].Trim());
        }
    }

    private static void ParseExitRule(string line, StrategyConfig config, List<TakeProfitTarget> takeProfits)
    {
        if (line.StartsWith("stop_loss ", StringComparison.OrdinalIgnoreCase))
        {
            config.StopLossPercent = ParsePercent(line["stop_loss ".Length..]);
            return;
        }

        if (line.StartsWith("take_profit_", StringComparison.OrdinalIgnoreCase))
        {
            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length >= 5)
            {
                takeProfits.Add(new TakeProfitTarget
                {
                    Percent = ParsePercent(parts[1]),
                    Weight = ParsePercent(parts[3]) / 100m
                });
            }

            return;
        }

        if (line.StartsWith("close_all_at ", StringComparison.OrdinalIgnoreCase))
        {
            var timeText = line["close_all_at ".Length..].Trim();
            config.CloseAllAt = TimeOnly.ParseExact(timeText, "H:mm", CultureInfo.InvariantCulture);
        }
    }

    private static void ParseRiskRule(string line, StrategyConfig config)
    {
        if (line.StartsWith("max_daily_loss ", StringComparison.OrdinalIgnoreCase))
        {
            var value = line["max_daily_loss ".Length..].Replace(" USD", string.Empty, StringComparison.OrdinalIgnoreCase).Trim();
            config.MaxDailyLossUsd = decimal.Parse(value, CultureInfo.InvariantCulture);
            return;
        }

        if (line.StartsWith("max_concurrent_trades ", StringComparison.OrdinalIgnoreCase))
        {
            config.MaxConcurrentTrades = int.Parse(line["max_concurrent_trades ".Length..].Trim(), CultureInfo.InvariantCulture);
            return;
        }

        if (line.StartsWith("max_trades_per_day ", StringComparison.OrdinalIgnoreCase))
        {
            config.MaxTradesPerDay = int.Parse(line["max_trades_per_day ".Length..].Trim(), CultureInfo.InvariantCulture);
            return;
        }

        if (line.StartsWith("no_reentry_after_stop ", StringComparison.OrdinalIgnoreCase))
        {
            config.NoReentryAfterStop = bool.Parse(line["no_reentry_after_stop ".Length..].Trim());
        }
    }

    private static string ExtractQuotedValue(string value)
    {
        var trimmed = value.Trim();
        if (trimmed.StartsWith('"') && trimmed.EndsWith('"') && trimmed.Length >= 2)
        {
            return trimmed[1..^1];
        }

        return trimmed;
    }

    private static decimal ParsePercent(string value)
    {
        var cleaned = value.Trim().TrimEnd('%');
        return decimal.Parse(cleaned, CultureInfo.InvariantCulture);
    }
}
