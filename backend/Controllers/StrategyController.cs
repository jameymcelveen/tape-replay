using Microsoft.AspNetCore.Mvc;
using TapeReplay.Api.Interfaces;
using TapeReplay.Api.Models;

namespace TapeReplay.Api.Controllers;

/// <summary>
/// Strategy DSL parsing and generation endpoints.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public sealed class StrategyController(IStrategyParser parser) : ControllerBase
{
    /// <summary>
    /// Parses a strategy DSL string into a <see cref="StrategyConfig"/>.
    /// </summary>
    [HttpPost("parse")]
    public ActionResult<StrategyConfig> Parse([FromBody] ParseDslRequest request)
    {
        try
        {
            return Ok(parser.Parse(request.Dsl));
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Generates DSL text from a strategy configuration object.
    /// </summary>
    [HttpPost("generate")]
    public ActionResult<GenerateDslResponse> Generate([FromBody] StrategyConfig config)
    {
        var dsl = parser.Generate(config);
        return Ok(new GenerateDslResponse { Dsl = dsl });
    }
}

/// <summary>
/// Request body for DSL parsing.
/// </summary>
public sealed class ParseDslRequest
{
    public required string Dsl { get; init; }
}

/// <summary>
/// Response containing generated DSL text.
/// </summary>
public sealed class GenerateDslResponse
{
    public required string Dsl { get; init; }
}
