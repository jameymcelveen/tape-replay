using TapeReplay.Api.Models;

namespace TapeReplay.Api.Services;

/// <summary>
/// Resolves trade cost configuration with pessimistic defaults when not supplied.
/// </summary>
public static class TradeCostDefaults
{
    public static TradeCostConfig Resolve(TradeCostConfig? costs) => costs ?? new TradeCostConfig();
}
