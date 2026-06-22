namespace ITHelpDeskDb.Models.DTOs.Responses;

public class DashboardStatsResponse
{
    public int TotalTickets { get; set; }
    public int OpenTickets { get; set; }
    public int InProgressTickets { get; set; }
    public int ResolvedTickets { get; set; }
    public int ClosedTickets { get; set; }
    public int EscalatedTickets { get; set; }

    public List<TicketsPerDayPoint> TicketsOverTime { get; set; } = new();
    public List<StatusBreakdownPoint> StatusBreakdown { get; set; } = new();
    public List<PriorityBreakdownPoint> PriorityBreakdown { get; set; } = new();
    public List<CategoryBreakdownPoint> CategoryBreakdown { get; set; } = new();
}

public class TicketsPerDayPoint
{
    public string Date { get; set; } = "";
    public int Created { get; set; }
    public int Resolved { get; set; }
}

public class StatusBreakdownPoint
{
    public string Status { get; set; } = "";
    public int Count { get; set; }
}

public class PriorityBreakdownPoint
{
    public string Priority { get; set; } = "";
    public int Count { get; set; }
}

public class CategoryBreakdownPoint
{
    public string Category { get; set; } = "";
    public int Count { get; set; }
}