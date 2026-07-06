namespace ITHelpDeskDb.Models.DTOs.Requests;

public record ParseTicketRequest(string RawText, List<string>? AvailableManagers);