namespace ITHelpDeskDb.Models;

public class TicketAttachment
{
    public int Id { get; set; }
    public string? FileName { get; set; }
    public string? ContentType { get; set; }
    public byte[]? Content { get; set; }

    public int TicketId { get; set; }
    public Ticket Ticket { get; set; } = null!;

    public int UploadedById { get; set; }
    public User UploadedBy { get; set; } = null!;
}