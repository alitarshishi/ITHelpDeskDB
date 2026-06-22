using ITHelpDeskDb.Data;
using ITHelpDeskDb.Models;
using ITHelpDeskDb.Models.DTOs.Responses;
using ITHelpDeskDb.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ITHelpDeskDb.Controllers.Api;

[ApiController]
[Route("api/itagent")]
[Authorize]
public class ITAgentController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly NotificationService _notifier;

    public ITAgentController(AppDbContext db, NotificationService notifier) {
        _db = db;
        _notifier = notifier;
    }

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
        if (ticket.AssignedByManagerId != null &&
        (newStatus.Name == "Resolved" || newStatus.Name == "Escalated"))
            {
                await _notifier.NotifyAsync(
                    ticket.AssignedByManagerId.Value,
                    ticket.Id,
                    newStatus.Name,
                    newStatus.Name == "Resolved"
                        ? $"TKT-{ticket.Id:D4} was resolved by {agent?.UserName}"
                     : $"TKT-{ticket.Id:D4} was escalated by {agent?.UserName}"
            );
        }

        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("{id}/note")]
    public async Task<IActionResult> AddNote(int id, [FromBody] AgentNoteRequest req)
    {
        if (req is null || string.IsNullOrWhiteSpace(req.Text))
            return BadRequest(new { message = "Note text is required." });

        if (!int.TryParse(User.FindFirst("sub")?.Value, out var myId))
            return Unauthorized();

        var ticket = await _db.Tickets.FindAsync(id);
        if (ticket == null) return NotFound();
        if (ticket.AssignedToId != myId) return Forbid();

        var agent = await _db.Users.FindAsync(myId);

        _db.ActivityLogs.Add(new ActivityLog
        {
            TicketId = id,
            UserId = myId,
            EventType = "AgentNote",
            Action = $"{agent?.UserName}: {req.Text}",
            Timestamp = DateTime.UtcNow,
        });

        await _db.SaveChangesAsync();
        return NoContent();
    }



}


public record AgentStatusRequest(int StatusId);
public record AgentNoteRequest(string Text);