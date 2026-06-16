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

            if (line.StartsWith("set position_size to ", StringComparison.OrdinalIgnoreCase))
            {
                var value = line["set position_size to ".Length..].Replace(" USD", string.Empty, StringComparison.OrdinalIgnoreCase).Trim();
                config.PositionSizeUsd = decimal.Parse(value, CultureInfo.InvariantCulture);
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
        builder.AppendLine("on price_breaks_above_daily_high");
        builder.AppendLine();
        builder.AppendLine(CultureInfo.InvariantCulture, $"set position_size to {config.PositionSizeUsd:0} USD");
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
        builder.AppendLine("}");

        return builder.ToString().TrimEnd();
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
