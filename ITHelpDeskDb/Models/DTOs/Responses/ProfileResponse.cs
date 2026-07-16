namespace ITHelpDeskDb.Models.DTOs.Responses;

public class ProfileResponse
{
    public int Id { get; set; }
    public string? UserName { get; set; }
    public string? Email { get; set; }
    public string? Role { get; set; }
    public bool HasAvatar { get; set; }
}