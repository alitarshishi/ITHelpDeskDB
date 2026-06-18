namespace ITHelpDeskDb.Models;

public class ActivityLog
{
    public int Id { get; set; }
    public string? Action { get; set; }

    public string? EventType { get; set; }
    public DateTime Timestamp { get; set; }

    public int? TicketId { get; set; }
    public Ticket? Ticket { get; set; }

    public int? UserId { get; set; }
    public User? User { get; set; }
}