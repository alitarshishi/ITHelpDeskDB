using ITHelpDeskDb.Data;
using ITHelpDeskDb.Models.DTOs.Responses;
using Microsoft.EntityFrameworkCore;

namespace ITHelpDeskDb.Services;

public class DashboardService
{
    private readonly AppDbContext _db;
    public DashboardService(AppDbContext db) => _db = db;

    public async Task<DashboardStatsResponse> GetStatsAsync(
    string period, string role, int userId)
    {
        var since = period switch
        {
            "2weeks" => DateTime.UtcNow.AddDays(-14),
            "month" => DateTime.UtcNow.AddMonths(-1),
            _ => DateTime.UtcNow.AddDays(-7),
        };

        // Base filter — no Include() needed since we project Name columns directly
        var baseQuery = _db.Tickets.Where(t => t.DateCreated >= since);

        if (role == "Manager")
            baseQuery = baseQuery.Where(t =>
                t.AssignedToId == userId || t.AssignedByManagerId == userId);

        // ── Scalar counts — simple COUNT WHERE queries in SQL ──
        var total = await baseQuery.CountAsync();
        var open = await baseQuery.CountAsync(t => t.Status.Name == "Open");
        var inProgress = await baseQuery.CountAsync(t => t.Status.Name == "In Progress");
        var resolved = await baseQuery.CountAsync(t => t.Status.Name == "Resolved");
        var closed = await baseQuery.CountAsync(t => t.Status.Name == "Closed");
        var escalated = await baseQuery.CountAsync(t => t.Status.Name == "Escalated");

        // ── Status breakdown — GROUP BY translates fine ──
        var statusBreakdown = await baseQuery
            .GroupBy(t => t.Status.Name)
            .Select(g => new StatusBreakdownPoint { Status = g.Key, Count = g.Count() })
            .ToListAsync();

        // ── Priority breakdown ──
        var priorityBreakdown = await baseQuery
            .GroupBy(t => t.Priority.Name)
            .Select(g => new PriorityBreakdownPoint { Priority = g.Key, Count = g.Count() })
            .ToListAsync();

        // ── Category breakdown ──
        var categoryBreakdown = await baseQuery
            .GroupBy(t => t.Category.Name)
            .Select(g => new CategoryBreakdownPoint { Category = g.Key, Count = g.Count() })
            .ToListAsync();

        // ── Tickets over time ──
        // EF Core can't translate .Date in GroupBy or ToString("MMM dd") to SQL Server.
        // Fix: project only the two datetime columns we need into C#, then group in memory.
        // This is safe — we're only pulling two DateTime columns, not full entity graphs.
        var rawDates = await baseQuery
            .Select(t => new
            {
                t.DateCreated,
                t.DateResolved,
            })
            .ToListAsync();

        var ticketsOverTime = rawDates
            .GroupBy(t => t.DateCreated.Date)
            .Select(g => new TicketsPerDayPoint
            {
                Date = g.Key.ToString("MMM dd"),
                Created = g.Count(),
                Resolved = g.Count(t => t.DateResolved != null
                               && t.DateResolved.Value.Date == g.Key),
            })
            .OrderBy(p => DateTime.ParseExact(p.Date, "MMM dd",
                           System.Globalization.CultureInfo.InvariantCulture))
            .ToList();

        return new DashboardStatsResponse
        {
            TotalTickets = total,
            OpenTickets = open,
            InProgressTickets = inProgress,
            ResolvedTickets = resolved,
            ClosedTickets = closed,
            EscalatedTickets = escalated,
            StatusBreakdown = statusBreakdown,
            PriorityBreakdown = priorityBreakdown,
            CategoryBreakdown = categoryBreakdown,
            TicketsOverTime = ticketsOverTime,
        };
    }
}