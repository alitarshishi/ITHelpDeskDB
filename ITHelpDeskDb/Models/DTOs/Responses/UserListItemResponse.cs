namespace ITHelpDeskDb.Models.DTOs.Responses;

// Lightweight shape for dropdown/lookup endpoints — name + id only, no PII beyond username.
public class UserListItemResponse
{
    public int Id { get; set; }
    public string UserName { get; set; } = "";
}
