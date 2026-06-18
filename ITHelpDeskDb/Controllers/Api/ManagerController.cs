using ITHelpDeskDb.Data;
using ITHelpDeskDb.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ITHelpDeskDb.Models.DTOs.Requests;

namespace ITHelpDeskDb.Controllers.Api;

[ApiController]
[Route("api/manager")]
[Authorize(Roles = "Manager")]
public class ManagerController : ControllerBase
{
    private readonly AppDbContext _db;

    public ManagerController(AppDbContext db) => _db = db;

    // ── GET /api/manager/team-tickets ──────────────────
    [HttpGet("team-tickets")]
    public async Task<IActionResult> TeamTickets()
    {
        var managerId = int.Parse(User.FindFirst("sub")!.Value);

        var tickets = await _db.Tickets
            .Include(t => t.SubmittedBy)
            .Include(t => t.AssignedTo)
            .Include(t => t.Priority)
            .Include(t => t.Category)
            .Include(t => t.Status)
            .Where(t => t.AssignedToId == managerId      // assigned TO manager
                     || t.AssignedByManagerId == managerId) // OR assigned BY manager to agent
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
            AssignedByManagerId = t.AssignedByManagerId,
        }));
    }
    

    //  PATCH /api/manager/{id}/update 
    [HttpPatch("{id}/update")]
    public async Task<IActionResult> UpdateTicket(int id, [FromBody] ManagerUpdateRequest req)
    {
        var managerId = int.Parse(User.FindFirst("sub")!.Value);
        var manager = await _db.Users.FindAsync(managerId);
        var ticket = await _db.Tickets.FindAsync(id);
        if (ticket == null) return NotFound();

        if (req.PriorityId != null) ticket.PriorityId = req.PriorityId.Value;
        if (req.AssignedToId != null)
        {
            ticket.AssignedToId = req.AssignedToId;
            ticket.AssignedByManagerId = managerId; 
        }
        if (req.StatusId != null && req.StatusId != ticket.StatusId)
        {
            var newStatus = await _db.Statuses.FindAsync(req.StatusId);
            var oldStatus = ticket.Status?.Name ?? "Unknown";

            ticket.StatusId = req.StatusId.Value;

            _db.ActivityLogs.Add(new ActivityLog
            {
                TicketId = ticket.Id,
                UserId = managerId,
                EventType = req.StatusId == 4 ? "Closed" : "StatusChanged",
                Action = $"Status changed from {oldStatus} to {newStatus?.Name} by {manager?.UserName}",
                Timestamp = DateTime.UtcNow,
            });
        }

        
        if (req.AssignedToId != null && req.AssignedToId != ticket.AssignedToId)
        {
            var newAgent = await _db.Users.FindAsync(req.AssignedToId);
            var oldAgentName = ticket.AssignedTo?.UserName ?? "Unassigned";

            ticket.AssignedToId = req.AssignedToId;
            ticket.AssignedByManagerId = managerId;

            _db.ActivityLogs.Add(new ActivityLog
            {
                TicketId = ticket.Id,
                UserId = managerId,
                EventType = "Reassigned",
                Action = $"Ticket reassigned from {oldAgentName} to {newAgent?.UserName} by {manager?.UserName}",
                Timestamp = DateTime.UtcNow,
            });
        }

        await _db.SaveChangesAsync();
        return NoContent();
    }

    //  GET /api/manager/reports/status-counts 
    [HttpGet("reports/status-counts")]
    public async Task<IActionResult> StatusCounts()
    {
        var counts = await _db.Tickets
            .GroupBy(t => t.StatusId)
            .Select(g => new { StatusId = g.Key, Count = g.Count() })
            .ToListAsync();

        return Ok(counts);
    }

    //  GET /api/manager/reports/avg-resolution-hours 
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

