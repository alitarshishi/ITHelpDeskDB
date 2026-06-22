using ITHelpDeskDb.Data;
using ITHelpDeskDb.Models.DTOs.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ITHelpDeskDb.Controllers.Api;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin,Manager")]
public class DashboardController : ControllerBase
{
    private readonly AppDbContext _db;
    public DashboardController(AppDbContext db) => _db = db;

    //  GET /api/dashboard/stats?period=week|2weeks|month 
    [HttpGet("stats")]
    public async Task<IActionResult> GetStats([FromQuery] string period = "week")
    {
        var since = period switch
        {
            "2weeks" => DateTime.UtcNow.AddDays(-14),
            "month" => DateTime.UtcNow.AddMonths(-1),
            _ => DateTime.UtcNow.AddDays(-7),
        };

        var role = User.FindFirst("role")?.Value ?? "";
        var userId = int.Parse(User.FindFirst("sub")!.Value);

        var query = _db.Tickets
            .Include(t => t.Status)
            .Include(t => t.Priority)
            .Include(t => t.Category)
            .Where(t => t.DateCreated >= since);

        // Manager only sees tickets they're involved with; Admin sees everything
        if (role == "Manager")
        {
            query = query.Where(t => t.AssignedToId == userId || t.AssignedByManagerId == userId);
        }

        var tickets = await query.ToListAsync();

        var result = new DashboardStatsResponse
        {
            TotalTickets = tickets.Count,
            OpenTickets = tickets.Count(t => t.Status?.Name == "Open"),
            InProgressTickets = tickets.Count(t => t.Status?.Name == "In Progress"),
            ResolvedTickets = tickets.Count(t => t.Status?.Name == "Resolved"),
            ClosedTickets = tickets.Count(t => t.Status?.Name == "Closed"),
            EscalatedTickets = tickets.Count(t => t.Status?.Name == "Escalated"),

            StatusBreakdown = tickets
                .GroupBy(t => t.Status?.Name ?? "Unknown")
                .Select(g => new StatusBreakdownPoint { Status = g.Key, Count = g.Count() })
                .ToList(),

            PriorityBreakdown = tickets
                .GroupBy(t => t.Priority?.Name ?? "Unknown")
                .Select(g => new PriorityBreakdownPoint { Priority = g.Key, Count = g.Count() })
                .ToList(),

            CategoryBreakdown = tickets
                .GroupBy(t => t.Category?.Name ?? "Unknown")
                .Select(g => new CategoryBreakdownPoint { Category = g.Key, Count = g.Count() })
                .ToList(),

            TicketsOverTime = tickets
                .GroupBy(t => t.DateCreated.Date)
                .Select(g => new TicketsPerDayPoint
                {
                    Date = g.Key.ToString("MMM dd"),
                    Created = g.Count(),
                    Resolved = g.Count(t => t.DateResolved != null && t.DateResolved.Value.Date == g.Key),
                })
                .OrderBy(p => DateTime.Parse(p.Date))
                .ToList(),
        };

        return Ok(result);
    }
}