namespace TapeReplay.Api.Models.DataDistribution;

/// <summary>
/// Machine role for market data recording and CDN sync.
/// </summary>
public enum DataDistributionRole
{
    Publisher,
    Subscriber,
    Both
}
