namespace ITHelpDeskDb.Models.DTOs.Responses;

public class ItAgentListItemResponse
{
    public int Id { get; set; }
    public string UserName { get; set; } = "";
    public int OpenTicketCount { get; set; }
}