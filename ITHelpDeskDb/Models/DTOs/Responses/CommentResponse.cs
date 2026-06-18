namespace ITHelpDeskDb.Models.DTOs.Responses;

public class CommentResponse
{
    public int Id { get; set; }
    public string? Text { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? AuthorName { get; set; }
    public int AuthorId { get; set; }
}