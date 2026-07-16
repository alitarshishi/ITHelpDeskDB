namespace ITHelpDeskDb.Models.DTOs.Requests;

public record ChatMessageDto(string Role, string Content); // Role: "user" | "assistant"

public record ChatRequest(List<ChatMessageDto> Messages);