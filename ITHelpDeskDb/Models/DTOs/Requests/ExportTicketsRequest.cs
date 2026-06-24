namespace ITHelpDeskDb.Models.DTOs.Requests;

public record ExportTicketsRequest(string Period); // "week" | "2weeks" | "month" | "all"