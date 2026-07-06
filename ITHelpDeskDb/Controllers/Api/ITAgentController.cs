using ITHelpDeskDb.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ITHelpDeskDb.Controllers.Api;

[ApiController]
[Route("api/itagent")]
[Authorize]
public class ITAgentController : ControllerBase
{
    private readonly TicketService _tickets;
    public ITAgentController(TicketService tickets) => _tickets = tickets;

    [HttpGet("assigned")]
    public async Task<IActionResult> GetAssigned()
    {
        if (!int.TryParse(User.FindFirst("sub")?.Value, out var myId))
            return Unauthorized();

        return Ok(await _tickets.GetAssignedToAgentAsync(myId));
    }

    [HttpPatch("{id}/status")]
    public async Task<IActionResult> UpdateStatus(int id, [FromBody] AgentStatusRequest req)
    {
        if (!int.TryParse(User.FindFirst("sub")?.Value, out var myId))
            return Unauthorized();

        var (found, allowed, error) = await _tickets.UpdateStatusAsync(id, req.StatusId, myId);
        if (!found) return NotFound();
        if (!allowed) return error == "Forbidden" ? Forbid() : BadRequest(new { message = error });
        return NoContent();
    }

    [HttpPost("{id}/note")]
    public async Task<IActionResult> AddNote(int id, [FromBody] AgentNoteRequest req)
    {
        if (req is null || string.IsNullOrWhiteSpace(req.Text))
            return BadRequest(new { message = "Note text is required." });

        if (!int.TryParse(User.FindFirst("sub")?.Value, out var myId))
            return Unauthorized();

        var (found, allowed) = await _tickets.AddNoteAsync(id, req.Text, myId);
        if (!found) return NotFound();
        if (!allowed) return Forbid();
        return NoContent();
    }
}

public record AgentStatusRequest(int StatusId);
public record AgentNoteRequest(string Text);