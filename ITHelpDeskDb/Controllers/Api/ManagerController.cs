using ITHelpDeskDb.Data;
using ITHelpDeskDb.Models;
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
            .Where(t => t.AssignedToId == managerId)  // only this manager's tickets
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
        }));
    }

    // ── GET /api/users/managers ────────────────────────
    [HttpGet("~/api/users/managers")]
    [Authorize]
    public async Task<IActionResult> GetManagers()
    {
        var managers = await _db.Users
            .Include(u => u.Role)
            .Where(u => u.Role.Name == "Manager")
            .Select(u => new { u.Id, u.UserName })
            .ToListAsync();

        return Ok(managers);
    }

    // ── GET /api/users/itagents ────────────────────────
    [HttpGet("~/api/users/itagents")]
    [Authorize]
    public async Task<IActionResult> GetItAgents()
    {
        var agents = await _db.Users
            .Include(u => u.Role)
            .Where(u => u.Role.Name == "IT Agent")
            .Select(u => new { u.Id, u.UserName })
            .ToListAsync();

        return Ok(agents);
    }

    // ── PATCH /api/manager/{id}/update ────────────────
    [HttpPatch("{id}/update")]
    public async Task<IActionResult> UpdateTicket(int id, [FromBody] ManagerUpdateRequest req)
    {
        var ticket = await _db.Tickets.FindAsync(id);
        if (ticket == null) return NotFound();

        if (req.PriorityId != null) ticket.PriorityId = req.PriorityId.Value;
        if (req.AssignedToId != null) ticket.AssignedToId = req.AssignedToId;
        if (req.StatusId != null) ticket.StatusId = req.StatusId.Value;

        _db.ActivityLogs.Add(new ActivityLog
        {
            Action = $"Manager updated ticket #{ticket.Id}",
            Timestamp = DateTime.UtcNow,
            TicketId = ticket.Id,
            UserId = int.TryParse(User.FindFirst("sub")?.Value, out var mId) ? mId : null  
        });

        await _db.SaveChangesAsync();
        return NoContent();
    }

    // ── GET /api/manager/reports/status-counts ────────
    [HttpGet("reports/status-counts")]
    public async Task<IActionResult> StatusCounts()
    {
        var counts = await _db.Tickets
            .GroupBy(t => t.StatusId)
            .Select(g => new { StatusId = g.Key, Count = g.Count() })
            .ToListAsync();

        return Ok(counts);
    }

    // ── GET /api/manager/reports/avg-resolution-hours ─
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