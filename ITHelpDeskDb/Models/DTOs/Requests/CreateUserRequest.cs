namespace ITHelpDeskDb.Models.DTOs.Requests;

public record CreateUserRequest(
    string UserName,
    string Email,
    int RoleId,
    string Password
);