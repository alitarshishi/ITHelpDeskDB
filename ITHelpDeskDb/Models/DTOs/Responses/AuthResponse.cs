namespace ITHelpDeskDb.Models.DTOs.Responses
{
    public class AuthResponse
    {
        public string Token { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public string RedirectUrl { get; set; } = string.Empty;
        public UserResponse User { get; set; } = null!;
    }
}
