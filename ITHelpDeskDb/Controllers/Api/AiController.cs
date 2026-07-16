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
    [HttpPost("chat")]
    [Authorize(Roles = "Employee")]
    public async Task<IActionResult> Chat([FromBody] ChatRequest req)
    {
        if (req is null || req.Messages == null || req.Messages.Count == 0)
            return BadRequest(new { message = "No messages provided." });

        // Cap history sent to the model to keep payloads reasonable
        var trimmed = req.Messages.Count > 20
            ? req.Messages.Skip(req.Messages.Count - 20).ToList()
            : req.Messages;

        var (result, error) = await _ai.ChatAsync(trimmed);

        if (error != null)
            return StatusCode(502, new { message = error });

        return Ok(result);
    }
}