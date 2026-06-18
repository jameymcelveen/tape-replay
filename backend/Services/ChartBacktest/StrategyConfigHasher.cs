using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using TapeReplay.Api.Models.ChartBacktest;

namespace TapeReplay.Api.Services.ChartBacktest;

/// <summary>
/// Stable hash for strategy configuration used as a cache key.
/// </summary>
public static class StrategyConfigHasher
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    /// <summary>
    /// Computes a SHA-256 hex digest over rule, params, scope, and shares.
    /// </summary>
    public static string Compute(StrategyHeatmapStrategyConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        var canonical = new
        {
            rule = config.Rule.Trim().ToLowerInvariant(),
            scope = config.Scope.Trim().ToLowerInvariant(),
            shares = config.Shares,
            @params = new
            {
                orMinutes = config.Params.OrMinutes,
                stopPct = config.Params.StopPct,
                targetPct = config.Params.TargetPct
            }
        };

        var json = JsonSerializer.Serialize(canonical, SerializerOptions);
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(json));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
