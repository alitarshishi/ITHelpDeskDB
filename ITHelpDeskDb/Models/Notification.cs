namespace ITHelpDeskDb.Models;

public class Notification
{
    public int Id { get; set; }
    public string? Message { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsRead { get; set; }          

    public int RecipientId { get; set; }      
    public User Recipient { get; set; } = null!;  

    public int? TicketId { get; set; }        
    public Ticket? Ticket { get; set; }       

    public string? Trigger { get; set; }      
}