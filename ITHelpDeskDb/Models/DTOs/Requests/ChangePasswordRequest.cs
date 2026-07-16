namespace ITHelpDeskDb.Models.DTOs.Requests;

public record ChangePasswordRequest(string CurrentPassword, string NewPassword);