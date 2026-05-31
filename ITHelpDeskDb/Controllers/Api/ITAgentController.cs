using ITHelpDeskDb.Data;
using ITHelpDeskDb.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ITHelpDeskDb.Controllers.Api;

[ApiController]
[Route("api/itagent")]
[Authorize(Roles = "ITAgent")]
public class ITAgentController : ControllerBase
{
    private readonly AppDbContext _db;

    public ITAgentController(AppDbContext db)
    {
        _db = db;
    }

    // Get tickets assigned to the current agent
    [HttpGet("assigned")]
    public async Task<IActionResult> GetAssigned()
    {
        if (!int.TryParse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var myId))
            return Unauthorized();

        var tickets = await _db.Tickets
            .Where(t => t.AssignedToId == myId)
            .Include(t => t.SubmittedBy)
            .Include(t => t.AssignedTo)
            .Include(t => t.Priority)
            .Include(t => t.Category)
            .Include(t => t.Status)
            .OrderByDescending(t => t.DateCreated)
            .ToListAsync();

        return Ok(tickets);
    }

    // Resolve a ticket (mark DateResolved and optionally update status)
    [HttpPost("{id}/resolve")]
    public async Task<IActionResult> ResolveTicket(int id, [FromBody] ResolveRequest? req)
    {
        if (!int.TryParse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var myId))
            return Unauthorized();

        var ticket = await _db.Tickets.FindAsync(id);
        if (ticket == null) return NotFound();
        if (ticket.AssignedToId != myId) return Forbid();

        if (ticket.DateResolved == null)
            ticket.DateResolved = DateTime.UtcNow;

        if (req?.StatusId != null)
            ticket.StatusId = req.StatusId.Value;

        // Add activity log
        _db.ActivityLogs.Add(new ActivityLog
        {
            Action = $"Ticket #{ticket.Id} resolved by agent {myId}",
            Timestamp = DateTime.UtcNow,
            TicketId = ticket.Id,
            UserId = myId
        });

        await _db.SaveChangesAsync();
        return NoContent();
    }

    // Add a comment to a ticket by the agent
    [HttpPost("{id}/comment")]
    public async Task<IActionResult> AddComment(int id, [FromBody] CommentRequest req)
    {
        if (req is null || string.IsNullOrWhiteSpace(req.Text)) return BadRequest();
        if (!int.TryParse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var myId))
            return Unauthorized();

        var ticket = await _db.Tickets.FindAsync(id);
        if (ticket == null) return NotFound();
        if (ticket.AssignedToId != myId) return Forbid();

        var comment = new TicketComment
        {
            TicketId = ticket.Id,
            Text = req.Text,
            CreatedAt = DateTime.UtcNow,
            AuthorId = myId
        };

        _db.TicketComments.Add(comment);
        _db.ActivityLogs.Add(new ActivityLog
        {
            Action = $"Agent {myId} commented on ticket #{ticket.Id}",
            Timestamp = DateTime.UtcNow,
            TicketId = ticket.Id,
            UserId = myId
        });

        await _db.SaveChangesAsync();
        return CreatedAtAction(null, new { id = comment.Id }, comment);
    }
}

public record ResolveRequest(int? StatusId);
public record CommentRequest(string Text);