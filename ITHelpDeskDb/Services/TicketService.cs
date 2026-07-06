using ITHelpDeskDb.Data;
using ITHelpDeskDb.Models;
using ITHelpDeskDb.Models.DTOs.Requests;
using ITHelpDeskDb.Models.DTOs.Responses;
using Microsoft.EntityFrameworkCore;

namespace ITHelpDeskDb.Services;

public class TicketService
{
    private readonly AppDbContext _db;
    private readonly NotificationService _notifier;

    public TicketService(AppDbContext db, NotificationService notifier)
    {
        _db = db;
        _notifier = notifier;
    }

    // ── Shared projection — used by all list/detail endpoints ──
    public IQueryable<TicketResponse> Project(IQueryable<Ticket>? source = null) =>
        (source ?? _db.Tickets).Select(t => new TicketResponse
        {
            Id = t.Id,
            Title = t.Title,
            Description = t.Description,
            DateCreated = t.DateCreated,
            DateResolved = t.DateResolved,
            StatusName = t.Status.Name,
            PriorityName = t.Priority.Name,
            CategoryName = t.Category.Name,
            AssignedToName = t.AssignedTo!.UserName,
            AssignedToId = t.AssignedToId,
            SubmittedByName = t.SubmittedBy.UserName,
            SubmittedById = t.SubmittedById,
            AssignedByManagerName = t.AssignedByManager!.UserName,
            AssignedByManagerId = t.AssignedByManagerId,
        });

    // ── Queries ────────────────────────────────────────────────

    public async Task<List<TicketResponse>> GetAllAsync() =>
        await Project().OrderByDescending(t => t.DateCreated).ToListAsync();

    public async Task<TicketResponse?> GetByIdAsync(int id) =>
        await Project(_db.Tickets.Where(t => t.Id == id)).FirstOrDefaultAsync();

    public async Task<List<TicketResponse>> GetBySubmitterAsync(int userId) =>
        await Project(_db.Tickets.Where(t => t.SubmittedById == userId))
            .OrderByDescending(t => t.DateCreated)
            .ToListAsync();

    public async Task<List<TicketResponse>> GetTeamTicketsAsync(int managerId) =>
        await Project(_db.Tickets.Where(t =>
                t.AssignedByManagerId == managerId || t.AssignedToId == managerId))
            .OrderByDescending(t => t.DateCreated)
            .ToListAsync();

    public async Task<List<TicketResponse>> GetAssignedToAgentAsync(int agentId) =>
        await Project(_db.Tickets.Where(t => t.AssignedToId == agentId))
            .OrderByDescending(t => t.DateCreated)
            .ToListAsync();

    public async Task<List<CommentResponse>> GetCommentsAsync(int ticketId) =>
        await _db.TicketComments
            .Where(c => c.TicketId == ticketId)
            .OrderBy(c => c.CreatedAt)
            .Select(c => new CommentResponse
            {
                Id = c.Id,
                Text = c.Text,
                CreatedAt = c.CreatedAt,
                AuthorName = c.Author.UserName,
                AuthorId = c.AuthorId,
            })
            .ToListAsync();

    public async Task<List<ActivityLogResponse>> GetActivityAsync(int ticketId) =>
        await _db.ActivityLogs
            .Where(a => a.TicketId == ticketId)
            .OrderBy(a => a.Timestamp)
            .Select(a => new ActivityLogResponse
            {
                Id = a.Id,
                Action = a.Action,
                EventType = a.EventType,
                Timestamp = a.Timestamp,
                UserName = a.User!.UserName,
            })
            .ToListAsync();

    public async Task<List<object>> GetAttachmentsAsync(int ticketId)
    {
        var result = await _db.TicketAttachments
            .Where(a => a.TicketId == ticketId)
            .OrderBy(a => a.Id)
            .Select(a => new
            {
                a.Id,
                a.FileName,
                a.ContentType,
                UploadedByName = a.UploadedBy.UserName,
            })
            .ToListAsync<object>();
        return result;
    }

    public async Task<(byte[]? content, string? contentType)> GetAttachmentBytesAsync(
        int attachmentId)
    {
        var a = await _db.TicketAttachments
            .Where(x => x.Id == attachmentId)
            .Select(x => new { x.Content, x.ContentType })
            .FirstOrDefaultAsync();

        return (a?.Content, a?.ContentType);
    }

    public async Task<bool> TicketExistsAsync(int id) =>
        await _db.Tickets.AnyAsync(t => t.Id == id);

    // ── Commands ────────────────────────────────────────────────

    public async Task<int> CreateAsync(CreateTicketRequest req, string submitterName)
    {
        var ticket = new Ticket
        {
            Title = req.Title,
            Description = req.Description,
            CategoryId = req.CategoryId,
            PriorityId = req.PriorityId,
            StatusId = req.StatusId,
            SubmittedById = req.SubmittedById,
            AssignedToId = null,
            AssignedByManagerId = req.AssignedToId,
            DateCreated = DateTime.UtcNow,
        };
        _db.Tickets.Add(ticket);
        await _db.SaveChangesAsync();

        _db.ActivityLogs.Add(new ActivityLog
        {
            TicketId = ticket.Id,
            UserId = req.SubmittedById,
            EventType = "Created",
            Action = $"Ticket created by {submitterName}",
            Timestamp = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync();

        if (req.AssignedToId != null)
            await _notifier.NotifyAsync(req.AssignedToId.Value, ticket.Id,
                "TicketCreated", $"New ticket TKT-{ticket.Id:D4} created by {submitterName}");

        return ticket.Id;
    }

    public async Task<bool> UpdateAsync(int id, UpdateTicketRequest req)
    {
        var ticket = await _db.Tickets.FindAsync(id);
        if (ticket == null) return false;

        ticket.Title = req.Title;
        ticket.Description = req.Description;
        ticket.PriorityId = req.PriorityId;
        ticket.CategoryId = req.CategoryId;
        ticket.StatusId = req.StatusId;
        ticket.AssignedToId = req.AssignedToId;

        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<(bool found, bool allowed, string? message)> DeleteAsync(
        int ticketId, int userId, string role)
    {
        var ticket = await _db.Tickets
            .Where(t => t.Id == ticketId)
            .Select(t => new { t.Id, t.SubmittedById, StatusName = t.Status.Name })
            .FirstOrDefaultAsync();

        if (ticket == null) return (false, false, null);

        if (role != "Admin")
        {
            if (ticket.SubmittedById != userId)
                return (true, false, "Forbidden");
            if (ticket.StatusName.ToLower() != "open")
                return (true, false, "You can only delete tickets with Open status.");
        }

        var entity = await _db.Tickets.FindAsync(ticketId);
        _db.Tickets.Remove(entity!);
        await _db.SaveChangesAsync();
        return (true, true, null);
    }

    public async Task<(CommentResponse? comment, string? error)> AddCommentAsync(
        int ticketId, string text, int authorId, string role)
    {
        var ticket = await _db.Tickets
            .Where(t => t.Id == ticketId)
            .Select(t => new { t.Id, t.SubmittedById, t.AssignedToId })
            .FirstOrDefaultAsync();
        if (ticket == null) return (null, "NotFound");

        bool canComment = ticket.SubmittedById == authorId
                       || ticket.AssignedToId == authorId
                       || role == "Admin";
        if (!canComment) return (null, "Forbidden");

        var authorName = await _db.Users
            .Where(u => u.Id == authorId)
            .Select(u => u.UserName)
            .FirstOrDefaultAsync();

        var comment = new TicketComment
        {
            TicketId = ticketId,
            Text = text,
            CreatedAt = DateTime.UtcNow,
            AuthorId = authorId,
        };
        _db.TicketComments.Add(comment);

        _db.ActivityLogs.Add(new ActivityLog
        {
            TicketId = ticketId,
            UserId = authorId,
            EventType = "Comment",
            Action = $"{authorName} added a comment",
            Timestamp = DateTime.UtcNow,
        });

        await _db.SaveChangesAsync();

        if (ticket.SubmittedById == authorId && ticket.AssignedToId != null)
            await _notifier.NotifyAsync(ticket.AssignedToId.Value, ticketId, "Comment",
                $"{authorName} commented on TKT-{ticketId:D4}");
        else if (ticket.AssignedToId == authorId)
            await _notifier.NotifyAsync(ticket.SubmittedById, ticketId, "Comment",
                $"{authorName} commented on TKT-{ticketId:D4}");

        return (new CommentResponse
        {
            Id = comment.Id,
            Text = comment.Text,
            CreatedAt = comment.CreatedAt,
            AuthorName = authorName,
            AuthorId = authorId,
        }, null);
    }

    public async Task<(object? result, string? error)> AddAttachmentAsync(
        int ticketId, IFormFile file, int uploaderId)
    {
        // ── Validation ─────────────────────────────────────
        const long maxSize = 10 * 1024 * 1024; // 10 MB

        var allowedTypes = new HashSet<string>
    {
        "image/png", "image/jpeg", "image/gif", "image/webp",
        "application/pdf",
        "application/msword",
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        "application/vnd.ms-excel",
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        "text/plain",
        "application/zip",
    };

        if (file == null || file.Length == 0)
            return (null, "No file provided.");

        if (file.Length > maxSize)
            return (null, "File size must be under 10 MB.");

        if (!allowedTypes.Contains(file.ContentType.ToLower()))
            return (null, $"File type '{file.ContentType}' is not allowed. Accepted: images, PDF, Word, Excel, text, zip.");

        // Also validate the extension matches the content type
        // (prevents renaming a .exe to .pdf)
        var allowedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".gif", ".webp",
        ".pdf", ".doc", ".docx", ".xls", ".xlsx",
        ".txt", ".zip",
    };

        var ext = Path.GetExtension(file.FileName);
        if (string.IsNullOrEmpty(ext) || !allowedExtensions.Contains(ext))
            return (null, $"File extension '{ext}' is not allowed.");


        var ticket = await _db.Tickets
            .Where(t => t.Id == ticketId)
            .Select(t => new { t.Id, t.SubmittedById, t.AssignedToId })
            .FirstOrDefaultAsync();
        if (ticket == null) return (null, "NotFound");

        using var ms = new MemoryStream();
        await file.CopyToAsync(ms);

        var attachment = new TicketAttachment
        {
            TicketId = ticketId,
            FileName = file.FileName,
            ContentType = file.ContentType,
            Content = ms.ToArray(),
            UploadedById = uploaderId,
        };
        _db.TicketAttachments.Add(attachment);

        var uploaderName = await _db.Users
            .Where(u => u.Id == uploaderId)
            .Select(u => u.UserName)
            .FirstOrDefaultAsync();

        _db.ActivityLogs.Add(new ActivityLog
        {
            TicketId = ticketId,
            UserId = uploaderId,
            EventType = "Attachment",
            Action = $"{uploaderName} added attachment \"{file.FileName}\"",
            Timestamp = DateTime.UtcNow,
        });

        await _db.SaveChangesAsync();

        if (ticket.SubmittedById == uploaderId && ticket.AssignedToId != null)
            await _notifier.NotifyAsync(ticket.AssignedToId.Value, ticketId, "Attachment",
                $"{uploaderName} added an attachment to TKT-{ticketId:D4}");
        else if (ticket.AssignedToId == uploaderId)
            await _notifier.NotifyAsync(ticket.SubmittedById, ticketId, "Attachment",
                $"{uploaderName} added an attachment to TKT-{ticketId:D4}");

        return (new
        {
            attachment.Id,
            attachment.FileName,
            attachment.ContentType,
            UploadedByName = uploaderName,
        }, null);
    }

    public async Task<(bool found, bool allowed, string? error)> UpdateStatusAsync(
        int ticketId, int newStatusId, int agentId)
    {
        var ticket = await _db.Tickets
            .Where(t => t.Id == ticketId)
            .Select(t => new
            {
                t.Id,
                t.AssignedToId,
                t.AssignedByManagerId,
                t.DateResolved,
                CurrentStatusName = t.Status.Name,
            })
            .FirstOrDefaultAsync();
        if (ticket == null) return (false, false, null);
        if (ticket.AssignedToId != agentId) return (true, false, "Forbidden");

        var agentName = await _db.Users
            .Where(u => u.Id == agentId)
            .Select(u => u.UserName)
            .FirstOrDefaultAsync();

        var newStatusName = await _db.Statuses
            .Where(s => s.Id == newStatusId)
            .Select(s => s.Name)
            .FirstOrDefaultAsync();
        if (newStatusName == null) return (true, false, "Invalid status.");

        var entity = await _db.Tickets.FindAsync(ticketId);
        entity!.StatusId = newStatusId;
        if (newStatusName == "Resolved" && entity.DateResolved == null)
            entity.DateResolved = DateTime.UtcNow;

        _db.ActivityLogs.Add(new ActivityLog
        {
            TicketId = ticketId,
            UserId = agentId,
            EventType = newStatusName switch
            {
                "Resolved" => "Resolved",
                "Escalated" => "Escalated",
                _ => "StatusChanged",
            },
            Action = $"Status changed from {ticket.CurrentStatusName} to {newStatusName} by {agentName}",
            Timestamp = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync();

        if (ticket.AssignedByManagerId != null &&
            (newStatusName == "Resolved" || newStatusName == "Escalated"))
        {
            await _notifier.NotifyAsync(
                ticket.AssignedByManagerId.Value, ticketId, newStatusName,
                newStatusName == "Resolved"
                    ? $"TKT-{ticketId:D4} was resolved by {agentName}"
                    : $"TKT-{ticketId:D4} was escalated by {agentName}"
            );
        }

        return (true, true, null);
    }

    public async Task<(bool found, bool allowed)> AddNoteAsync(
        int ticketId, string text, int agentId)
    {
        var ticket = await _db.Tickets
            .Where(t => t.Id == ticketId)
            .Select(t => new { t.AssignedToId })
            .FirstOrDefaultAsync();
        if (ticket == null) return (false, false);
        if (ticket.AssignedToId != agentId) return (true, false);

        var agentName = await _db.Users
            .Where(u => u.Id == agentId)
            .Select(u => u.UserName)
            .FirstOrDefaultAsync();

        _db.ActivityLogs.Add(new ActivityLog
        {
            TicketId = ticketId,
            UserId = agentId,
            EventType = "AgentNote",
            Action = $"{agentName}: {text}",
            Timestamp = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync();
        return (true, true);
    }

    public async Task<string?> GetStatusNameAsync(int statusId) =>
        await _db.Statuses
            .Where(s => s.Id == statusId)
            .Select(s => s.Name)
            .FirstOrDefaultAsync();

    public async Task<(bool found, string? error)> ManagerUpdateAsync(
        int ticketId, ManagerUpdateRequest req, int managerId, string managerName)
    {
        var ticket = await _db.Tickets
            .Include(t => t.Status)
            .Include(t => t.AssignedTo)
            .FirstOrDefaultAsync(t => t.Id == ticketId);
        if (ticket == null) return (false, null);

        if (req.PriorityId != null)
            ticket.PriorityId = req.PriorityId.Value;

        if (req.StatusId != null && req.StatusId != ticket.StatusId)
        {
            var currentStatusName = ticket.Status?.Name ?? "Unknown";
            if (currentStatusName != "Open" &&
                currentStatusName != "Resolved" &&
                currentStatusName != "Escalated")
                return (true, $"Manager can only change status when the ticket is Open, Resolved, or Escalated.");

            var newStatusName = await _db.Statuses
                .Where(s => s.Id == req.StatusId)
                .Select(s => s.Name)
                .FirstOrDefaultAsync();
            if (newStatusName == null) return (true, "Invalid status.");

            ticket.StatusId = req.StatusId.Value;
            _db.ActivityLogs.Add(new ActivityLog
            {
                TicketId = ticketId,
                UserId = managerId,
                EventType = newStatusName == "Closed" ? "Closed" : "StatusChanged",
                Action = $"Status changed from {currentStatusName} to {newStatusName} by {managerName}",
                Timestamp = DateTime.UtcNow,
            });

            if (newStatusName == "Closed")
                await _notifier.NotifyAsync(ticket.SubmittedById, ticketId, "Closed",
                    $"Your ticket TKT-{ticketId:D4} was closed");
        }

        if (req.AssignedToId != null && req.AssignedToId != ticket.AssignedToId)
        {
            var newAgentName = await _db.Users
                .Where(u => u.Id == req.AssignedToId)
                .Select(u => u.UserName)
                .FirstOrDefaultAsync();

            var wasUnassigned = ticket.AssignedToId == null;
            var oldAgentId = ticket.AssignedToId;
            var oldAgentName = ticket.AssignedTo?.UserName ?? "Unassigned";

            ticket.AssignedToId = req.AssignedToId;
            ticket.AssignedByManagerId = managerId;

            _db.ActivityLogs.Add(new ActivityLog
            {
                TicketId = ticketId,
                UserId = managerId,
                EventType = wasUnassigned ? "Assigned" : "Reassigned",
                Action = wasUnassigned
                    ? $"Ticket assigned to {newAgentName} by {managerName}"
                    : $"Ticket reassigned from {oldAgentName} to {newAgentName} by {managerName}",
                Timestamp = DateTime.UtcNow,
            });

            await _notifier.NotifyAsync(req.AssignedToId.Value, ticketId, "Assigned",
                $"You were assigned TKT-{ticketId:D4}");

            if (!wasUnassigned && oldAgentId != null)
                await _notifier.NotifyAsync(oldAgentId.Value, ticketId, "Reassigned",
                    $"TKT-{ticketId:D4} was reassigned to someone else");
        }

        await _db.SaveChangesAsync();
        return (true, null);
    }
}