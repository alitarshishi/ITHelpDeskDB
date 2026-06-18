namespace ITHelpDeskDb.Models.DTOs.Requests;

public record CreateTicketRequest(
    string Title,
    string Description,
    int CategoryId,
    int PriorityId,
    int StatusId,
    int SubmittedById,
    int? AssignedToId
);
