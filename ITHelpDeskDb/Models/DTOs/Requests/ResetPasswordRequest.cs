namespace ITHelpDeskDb.Models.DTOs.Requests;

public record ResetPasswordRequest(string Token, string NewPassword);