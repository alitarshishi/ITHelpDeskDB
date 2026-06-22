using ITHelpDeskDb.Data;
using ITHelpDeskDb.Models;
using ITHelpDeskDb.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ITHelpDeskDb.Controllers.Api;

[ApiController]
[Route("api/manager")]
[Authorize(Roles = "Manager")]
public class ManagerController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly NotificationService _notifier;

    public ManagerController(AppDbContext db, NotificationService notifier)
    {
        _db = db;
        _notifier = notifier;
    }

    // ── GET /api/manager/team-tickets ──────────────────
    [HttpGet("team-tickets")]
    public async Task<IActionResult> TeamTickets()
    {
        var managerId = int.Parse(User.FindFirst("sub")!.Value);

        var tickets = await _db.Tickets
            .Include(t => t.SubmittedBy)
            .Include(t => t.AssignedTo)
            .Include(t => t.AssignedByManager)
            .Include(t => t.Priority)
            .Include(t => t.Category)
            .Include(t => t.Status)
            .Where(t => t.AssignedToId == managerId || t.AssignedByManagerId == managerId)
            .OrderByDescending(t => t.DateCreated)
            .ToListAsync();

        return Ok(tickets.Select(t => new
        {
            t.Id,
            t.Title,
            t.Description,
            t.DateCreated,
            StatusName = t.Status?.Name,
            PriorityName = t.Priority?.Name,
            CategoryName = t.Category?.Name,
            AssignedToName = t.AssignedTo?.UserName,
            AssignedToId = t.AssignedToId,
            SubmittedByName = t.SubmittedBy?.UserName,
            AssignedByManagerName = t.AssignedByManager?.UserName,
            AssignedByManagerId = t.AssignedByManagerId,
        }));
    }

 


    // ── PATCH /api/manager/{id}/update ────────────────
    [HttpPatch("{id}/update")]
    public async Task<IActionResult> UpdateTicket(int id, [FromBody] ManagerUpdateRequest req)
    {
        var managerId = int.Parse(User.FindFirst("sub")!.Value);
        var manager = await _db.Users.FindAsync(managerId);

        var ticket = await _db.Tickets
                              .Include(t => t.Status)
                              .Include(t => t.AssignedTo)
                              .FirstOrDefaultAsync(t => t.Id == id);
        if (ticket == null) return NotFound();

        if (req.PriorityId != null)
            ticket.PriorityId = req.PriorityId.Value;

        // ── Status change — capture the name BEFORE any mutation ──
        if (req.StatusId != null && req.StatusId != ticket.StatusId)
        {
            var currentStatusName = ticket.Status?.Name ?? "Unknown";

            if (currentStatusName != "Open" && currentStatusName != "Resolved" && currentStatusName != "Escalated")
                return BadRequest(new { message = "Manager can only change status when the ticket is Open,Resolved or Escalated." });

            var newStatus = await _db.Statuses.FindAsync(req.StatusId);
            if (newStatus == null) return BadRequest(new { message = "Invalid status." });

            ticket.StatusId = req.StatusId.Value;

            _db.ActivityLogs.Add(new ActivityLog
            {
                TicketId = ticket.Id,
                UserId = managerId,
                EventType = newStatus.Name == "Closed" ? "Closed" : "StatusChanged",
                Action = $"Status changed from {currentStatusName} to {newStatus.Name} by {manager?.UserName}",
                Timestamp = DateTime.UtcNow,
            });
            if (newStatus.Name == "Closed")
            {
                await _notifier.NotifyAsync(
                    ticket.SubmittedById,
                    ticket.Id,
                    "Closed",
                    $"Your ticket TKT-{ticket.Id:D4} was closed"
                );
            }


        }

        // ── Reassign / first-time assign ──────────────────
        if (req.AssignedToId != null && req.AssignedToId != ticket.AssignedToId)
        {
            var newAgent = await _db.Users.FindAsync(req.AssignedToId);
            var wasUnassigned = ticket.AssignedToId == null;
            var oldAgentId = ticket.AssignedToId;
            var oldAgentName = ticket.AssignedTo?.UserName ?? "Unassigned";

            ticket.AssignedToId = req.AssignedToId;
            ticket.AssignedByManagerId = managerId;

            _db.ActivityLogs.Add(new ActivityLog
            {
                TicketId = ticket.Id,
                UserId = managerId,
                EventType = wasUnassigned ? "Assigned" : "Reassigned",
                Action = wasUnassigned
                    ? $"Ticket assigned to {newAgent?.UserName} by {manager?.UserName}"
                    : $"Ticket reassigned from {oldAgentName} to {newAgent?.UserName} by {manager?.UserName}",
                Timestamp = DateTime.UtcNow,
            });
            await _notifier.NotifyAsync(
                req.AssignedToId.Value,
                ticket.Id,
                "Assigned",
                $"You were assigned TKT-{ticket.Id:D4}"
                );

            if (!wasUnassigned && oldAgentId != null)
            {
                await _notifier.NotifyAsync(
                    oldAgentId.Value,
                    ticket.Id,
                    "Reassigned",
                    $"TKT-{ticket.Id:D4} was reassigned to someone else"
                );
            }
        }

        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpGet("reports/status-counts")]
    public async Task<IActionResult> StatusCounts()
    {
        var counts = await _db.Tickets
            .GroupBy(t => t.StatusId)
            .Select(g => new { StatusId = g.Key, Count = g.Count() })
            .ToListAsync();

        return Ok(counts);
    }

    [HttpGet("reports/avg-resolution-hours")]
    public async Task<IActionResult> AverageResolutionHours()
    {
        var resolved = await _db.Tickets
            .Where(t => t.DateResolved != null)
            .Select(t => new
            {
                t.Id,
                Hours = EF.Functions.DateDiffHour(t.DateCreated, t.DateResolved!.Value)
            })
            .ToListAsync();

        if (resolved.Count == 0) return Ok(new { averageHours = 0 });

        return Ok(new { averageHours = resolved.Average(r => r.Hours) });
    }
}

public record ManagerUpdateRequest(int? PriorityId, int? AssignedToId, int? StatusId);