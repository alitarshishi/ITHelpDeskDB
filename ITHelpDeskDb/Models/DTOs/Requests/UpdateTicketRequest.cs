namespace ITHelpDeskDb.Models.DTOs.Requests;


public record UpdateTicketRequest(
    string Title,
    string Description,
    int PriorityId,
    int CategoryId,
    int StatusId,
    int? AssignedToId
);
