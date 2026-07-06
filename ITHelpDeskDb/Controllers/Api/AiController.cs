using ITHelpDeskDb.Models.DTOs.Requests;
using ITHelpDeskDb.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ITHelpDeskDb.Controllers.Api;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AiController : ControllerBase
{
    private readonly AiService _ai;
    public AiController(AiService ai) => _ai = ai;

    [HttpPost("parse-ticket")]
    public async Task<IActionResult> ParseTicket([FromBody] ParseTicketRequest req)
    {
        if (req is null || string.IsNullOrWhiteSpace(req.RawText))
            return BadRequest(new { message = "No text provided." });

        var (result, error) = await _ai.ParseTicketAsync(req);

        if (error != null)
            return StatusCode(502, new { message = error });

        return Ok(result);
    }
}