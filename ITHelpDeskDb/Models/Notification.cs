namespace ITHelpDeskDb.Models;

public class Notification
{
    public int Id { get; set; }
    public string? Message { get; set; }
    public DateTime CreatedAt { get; set; }

    public int TicketId { get; set; }
    public Ticket Ticket { get; set; } = null!;

    public string? Trigger { get; set; }
}