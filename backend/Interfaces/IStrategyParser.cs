using TapeReplay.Api.Models;

namespace TapeReplay.Api.Interfaces;

/// <summary>
/// Converts strategy DSL text to and from <see cref="StrategyConfig"/>.
/// </summary>
public interface IStrategyParser
{
    StrategyConfig Parse(string dsl);

    string Generate(StrategyConfig config);
}
