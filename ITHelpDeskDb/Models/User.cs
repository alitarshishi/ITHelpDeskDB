namespace ITHelpDeskDb.Models;

public class User
{
    public int Id { get; set; }
    public string? UserName { get; set; }
    public string? Email { get; set; }
    // One-to-many: each User has a single Role
    public int RoleId { get; set; }
    public Role Role { get; set; } = null!;
    public ICollection<Ticket> SubmittedTickets { get; set; } = new List<Ticket>();
    public ICollection<Ticket> AssignedTickets { get; set; } = new List<Ticket>();
    public ICollection<TicketComment> Comments { get; set; } = new List<TicketComment>();
    public ICollection<ActivityLog> ActivityLogs { get; set; } = new List<ActivityLog>();
    public ICollection<TicketAttachment> UploadedAttachments { get; set; } = new List<TicketAttachment>();
}