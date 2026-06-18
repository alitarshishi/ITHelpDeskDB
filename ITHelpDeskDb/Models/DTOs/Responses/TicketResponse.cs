namespace ITHelpDeskDb.Models.DTOs.Responses;

public class TicketResponse
{
    public int Id { get; set; }
    public string? Title { get; set; }
    public string? Description { get; set; }
    public DateTime DateCreated { get; set; }
    public DateTime? DateResolved { get; set; }
    public string? StatusName { get; set; }
    public string? PriorityName { get; set; }
    public string? CategoryName { get; set; }
    public string? AssignedToName { get; set; }
    public int? AssignedToId { get; set; }
    public string? SubmittedByName { get; set; }
    public int SubmittedById { get; set; }
    public string? AssignedByManagerName { get; set; }   
    public int? AssignedByManagerId { get; set; }
}