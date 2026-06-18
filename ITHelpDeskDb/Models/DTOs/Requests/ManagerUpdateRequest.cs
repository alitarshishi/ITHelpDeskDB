namespace ITHelpDeskDb.Models.DTOs.Requests;

public record ManagerUpdateRequest(
    int? PriorityId,
    int? AssignedToId,
    int? StatusId
);