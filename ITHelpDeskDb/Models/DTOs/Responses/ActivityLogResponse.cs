namespace ITHelpDeskDb.Models.DTOs.Responses;

public class ActivityLogResponse
{
    public int Id { get; set; }
    public string? Action { get; set; }
    public string? EventType { get; set; }
    public DateTime Timestamp { get; set; }
    public string? UserName { get; set; }
}