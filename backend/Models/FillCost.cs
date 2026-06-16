namespace TapeReplay.Api.Models;

/// <summary>
/// Breakdown of a single fill after spread, slippage, and commission.
/// </summary>
public sealed class FillCost
{
    public decimal ReferencePrice { get; init; }

    public decimal FillPrice { get; init; }

    public int Shares { get; init; }

    public decimal Commission { get; init; }

    public decimal SpreadCost { get; init; }

    public decimal SlippageCost { get; init; }

    public decimal TotalCost { get; init; }
}
