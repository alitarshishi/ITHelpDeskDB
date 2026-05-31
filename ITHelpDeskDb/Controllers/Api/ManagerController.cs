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

    public ManagerController(AppDbContext db)
    {
        _db = db;
    }

    // Monitor all team tickets (optionally filter by agent)
    [HttpGet("team-tickets")]
    public async Task<IActionResult> TeamTickets([FromQuery] int? agentId)
    {
        var query = _db.Tickets
            .Include(t => t.SubmittedBy)
            .Include(t => t.AssignedTo)
            .Include(t => t.Priority)
            .Include(t => t.Category)
            .Include(t => t.Status)
            .AsQueryable();

        if (agentId != null)
            query = query.Where(t => t.AssignedToId == agentId);

        var tickets = await query.OrderByDescending(t => t.DateCreated).ToListAsync();
        return Ok(tickets);
    }

    // Simple report: counts grouped by status
    [HttpGet("reports/status-counts")]
    public async Task<IActionResult> StatusCounts()
    {
        var counts = await _db.Tickets
            .GroupBy(t => t.StatusId)
            .Select(g => new { StatusId = g.Key, Count = g.Count() })
            .ToListAsync();

        return Ok(counts);
    }

    // Report: average resolution time (hours) for resolved tickets
    [HttpGet("reports/avg-resolution-hours")]
    public async Task<IActionResult> AverageResolutionHours()
    {
        var resolved = await _db.Tickets
            .Where(t => t.DateResolved != null)
            .Select(t => new { t.Id, Hours = EF.Functions.DateDiffHour(t.DateCreated, t.DateResolved!.Value) })
            .ToListAsync();

        if (resolved.Count == 0) return Ok(new { averageHours = 0 });

        var avg = resolved.Average(r => r.Hours);
        return Ok(new { averageHours = avg });
    }

    // Manager can change ticket priority or reassign
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
            UserId = int.TryParse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var mId) ? mId : null
        });

        await _db.SaveChangesAsync();
        return NoContent();
    }
}

public record ManagerUpdateRequest(int? PriorityId, int? AssignedToId, int? StatusId);