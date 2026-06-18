using ITHelpDeskDb.Data;
using ITHelpDeskDb.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ITHelpDeskDb.Models.DTOs.Responses;

namespace ITHelpDeskDb.Controllers.Api;

[ApiController]
[Route("api/itagent")]
[Authorize]
public class ITAgentController : ControllerBase
{
    private readonly AppDbContext _db;

    public ITAgentController(AppDbContext db) => _db = db;

    [HttpGet("assigned")]
    public async Task<IActionResult> GetAssigned()
    {
        if (!int.TryParse(User.FindFirst("sub")?.Value, out var myId))  
            return Unauthorized();

        var tickets = await _db.Tickets
            .Where(t => t.AssignedToId == myId)
            .Include(t => t.SubmittedBy)
            .Include(t => t.AssignedTo)
            .Include(t => t.AssignedByManager)
            .Include(t => t.Priority)
            .Include(t => t.Category)
            .Include(t => t.Status)
            .OrderByDescending(t => t.DateCreated)
            .ToListAsync();

        return Ok(tickets.Select(t => new TicketResponse
        {
            Id = t.Id,
            Title = t.Title,
            Description = t.Description,
            DateCreated = t.DateCreated,
            DateResolved = t.DateResolved,
            StatusName = t.Status?.Name,
            PriorityName = t.Priority?.Name,
            CategoryName = t.Category?.Name,
            AssignedToName = t.AssignedTo?.UserName,
            AssignedToId = t.AssignedToId,
            SubmittedByName = t.SubmittedBy?.UserName,
            SubmittedById = t.SubmittedById,
            AssignedByManagerName = t.AssignedByManager?.UserName,
            AssignedByManagerId = t.AssignedByManagerId,
        }));
    }

    [HttpPost("{id}/resolve")]
    public async Task<IActionResult> ResolveTicket(int id, [FromBody] ResolveRequest? req)
    {
        if (!int.TryParse(User.FindFirst("sub")?.Value, out var myId))  
            return Unauthorized();

        var ticket = await _db.Tickets.FindAsync(id);
        if (ticket == null) return NotFound();
        if (ticket.AssignedToId != myId) return Forbid();

        if (ticket.DateResolved == null)
            ticket.DateResolved = DateTime.UtcNow;

        if (req?.StatusId != null)
            ticket.StatusId = req.StatusId.Value;

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

    [HttpPost("{id}/comment")]
    public async Task<IActionResult> AddComment(int id, [FromBody] CommentRequest req)
    {
        if (req is null || string.IsNullOrWhiteSpace(req.Text)) return BadRequest();
        if (!int.TryParse(User.FindFirst("sub")?.Value, out var myId))  // ✅ fixed
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
    // ── PATCH /api/itagent/{id}/status — agent marks Resolved ──
    [HttpPatch("{id}/status")]
    public async Task<IActionResult> UpdateStatus(int id, [FromBody] AgentStatusRequest req)
    {
        if (!int.TryParse(User.FindFirst("sub")?.Value, out var myId))
            return Unauthorized();

        var ticket = await _db.Tickets
            .Include(t => t.Status)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (ticket == null) return NotFound();
        if (ticket.AssignedToId != myId) return Forbid();

        var agent = await _db.Users.FindAsync(myId);
        var newStatus = await _db.Statuses.FindAsync(req.StatusId);
        if (newStatus == null) return BadRequest(new { message = "Invalid status." });

        var oldStatusName = ticket.Status?.Name ?? "Unknown";
        ticket.StatusId = req.StatusId;

        if (newStatus.Name == "Resolved" && ticket.DateResolved == null)
            ticket.DateResolved = DateTime.UtcNow;

        _db.ActivityLogs.Add(new ActivityLog
        {
            TicketId = ticket.Id,
            UserId = myId,
            EventType = newStatus.Name == "Resolved" ? "Resolved" : "StatusChanged",
            Action = $"Status changed from {oldStatusName} to {newStatus.Name} by {agent?.UserName}",
            Timestamp = DateTime.UtcNow,
        });

        await _db.SaveChangesAsync();
        return NoContent();
    }



}

public record ResolveRequest(int? StatusId);
public record CommentRequest(string Text);
public record AgentStatusRequest(int StatusId);