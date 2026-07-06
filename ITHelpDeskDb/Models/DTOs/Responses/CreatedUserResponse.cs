namespace ITHelpDeskDb.Models.DTOs.Responses;

public class CreatedUserResponse
{
    public int Id { get; set; }
    public string UserName { get; set; } = "";
    public string Email { get; set; } = "";
}