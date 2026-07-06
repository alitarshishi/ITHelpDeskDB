namespace ITHelpDeskDb.Models.DTOs.Responses;

public class ParsedTicketResponse
{
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string Category { get; set; } = "";
    public string Priority { get; set; } = "";
    public string? ManagerName { get; set; }   
}
