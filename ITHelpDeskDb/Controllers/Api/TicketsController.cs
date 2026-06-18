using ITHelpDeskDb.Data;
using ITHelpDeskDb.Models;
using ITHelpDeskDb.Models.DTOs.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ITHelpDeskDb.Models.DTOs.Requests;

namespace ITHelpDeskDb.Controllers.Api;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class TicketsController : ControllerBase
{
    private readonly AppDbContext _db;

    public TicketsController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var tickets = await _db.Tickets
            .Include(t => t.SubmittedBy)
            .Include(t => t.AssignedTo)
            .Include(t => t.AssignedByManager)
            .Include(t => t.Priority)
            .Include(t => t.Category)
            .Include(t => t.Status)
            .OrderByDescending(t => t.DateCreated)
            .ToListAsync();

        return Ok(tickets.Select(t => new TicketResponse
        {
            Id = t.Id,
            Title = t.Title,
            Description = t.Description,
            DateCreated = t.DateCreated,
            DateResolved = t.DateResolved,
            StatusName = t.Status?.Name,
            PriorityName = t.Priority?.Name,
            CategoryName = t.Category?.Name,
            AssignedToName = t.AssignedTo?.UserName,
            AssignedToId = t.AssignedToId,
            SubmittedByName = t.SubmittedBy?.UserName,
            SubmittedById = t.SubmittedById,
            AssignedByManagerName = t.AssignedByManager?.UserName,
            AssignedByManagerId = t.AssignedByManagerId,
        }));
    }


    [HttpGet("{id}")]
    public async Task<IActionResult> Get(int id)
    {
        var t = await _db.Tickets
            .Include(t => t.SubmittedBy)
            .Include(t => t.AssignedTo)
            .Include(t => t.AssignedByManager)
            .Include(t => t.Priority)
            .Include(t => t.Category)
            .Include(t => t.Status)
            .FirstOrDefaultAsync(t => t.Id == id);
        if (t == null) return NotFound();
        return Ok(new TicketResponse
        {
            Id = t.Id,
            Title = t.Title,
            Description = t.Description,
            DateCreated = t.DateCreated,
            DateResolved = t.DateResolved,
            StatusName = t.Status?.Name,
            PriorityName = t.Priority?.Name,
            CategoryName = t.Category?.Name,
            AssignedToName = t.AssignedTo?.UserName,
            AssignedToId = t.AssignedToId,
            SubmittedByName = t.SubmittedBy?.UserName,
            SubmittedById = t.SubmittedById,
            AssignedByManagerName = t.AssignedByManager?.UserName,
            AssignedByManagerId = t.AssignedByManagerId,
        });
    }
    

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateTicketRequest req)
    {
        if (req == null) return BadRequest();

        var ticket = new Ticket
        {
            Title = req.Title,
            Description = req.Description,
            CategoryId = req.CategoryId,
            PriorityId = req.PriorityId,
            StatusId = req.StatusId,
            SubmittedById = req.SubmittedById,
            AssignedToId = req.AssignedToId,
            DateCreated = DateTime.UtcNow,
        };

        _db.Tickets.Add(ticket);
        await _db.SaveChangesAsync();
        var submitter = await _db.Users.FindAsync(req.SubmittedById);
        _db.ActivityLogs.Add(new ActivityLog
        {
            TicketId = ticket.Id,
            UserId = req.SubmittedById,
            EventType = "Created",
            Action = $"Ticket created by {submitter?.UserName}",
            Timestamp = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { id = ticket.Id }, new { ticket.Id });
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] Ticket update)
    {
        var ticket = await _db.Tickets.FindAsync(id);
        if (ticket == null) return NotFound();

        ticket.Title = update.Title;
        ticket.Description = update.Description;
        ticket.PriorityId = update.PriorityId;
        ticket.CategoryId = update.CategoryId;
        ticket.StatusId = update.StatusId;
        ticket.AssignedToId = update.AssignedToId;

        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPatch("{id}/assign")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Assign(int id, [FromBody] AssignRequest req)
    {
        var ticket = await _db.Tickets.FindAsync(id);
        if (ticket == null) return NotFound();

        ticket.AssignedToId = req.UserId;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var userId = int.Parse(User.FindFirst("sub")!.Value);
        var role = User.FindFirst("role")?.Value;

        var ticket = await _db.Tickets
                              .Include(t => t.Status)
                              .FirstOrDefaultAsync(t => t.Id == id);

        if (ticket == null) return NotFound();

        // Admin can delete anything
        // Employee can only delete their own Open tickets
        if (role != "Admin")
        {
            if (ticket.SubmittedById != userId)
                return Forbid();

            if ((ticket.Status?.Name ?? "").ToLower() != "open")
                return BadRequest(new { message = "You can only delete tickets with Open status." });
        }

        _db.Tickets.Remove(ticket);
        await _db.SaveChangesAsync();
        return NoContent();
    }
    [HttpGet("my")]
    public async Task<IActionResult> GetMine()
    {
        var userId = int.Parse(User.FindFirst("sub")!.Value);

        var tickets = await _db.Tickets
            .Include(t => t.Status)
            .Include(t => t.Priority)
            .Include(t => t.Category)
            .Include(t => t.AssignedTo)
            .Include(t => t.AssignedByManager)   
            .Include(t => t.SubmittedBy)
            .Where(t => t.SubmittedById == userId)
            .OrderByDescending(t => t.DateCreated)
            .ToListAsync();

        return Ok(tickets.Select(t => new TicketResponse
        {
            Id = t.Id,
            Title = t.Title,
            Description = t.Description,
            DateCreated = t.DateCreated,
            DateResolved = t.DateResolved,
            StatusName = t.Status?.Name,
            PriorityName = t.Priority?.Name,
            CategoryName = t.Category?.Name,
            AssignedToName = t.AssignedTo?.UserName,
            AssignedToId = t.AssignedToId,
            SubmittedByName = t.SubmittedBy?.UserName,  
            SubmittedById = t.SubmittedById,
            AssignedByManagerName = t.AssignedByManager?.UserName,
            AssignedByManagerId = t.AssignedByManagerId,
        }));
    }

    // ── GET /api/tickets/{id}/comments ────────────────
    [HttpGet("{id}/comments")]
    public async Task<IActionResult> GetComments(int id)
    {
        var ticket = await _db.Tickets.FindAsync(id);
        if (ticket == null) return NotFound();

        var comments = await _db.TicketComments
            .Include(c => c.Author)
            .Where(c => c.TicketId == id)
            .OrderBy(c => c.CreatedAt)
            .ToListAsync();

        
        return Ok(comments.Select(c => new CommentResponse
        {
            Id = c.Id,
            Text = c.Text,
            CreatedAt = c.CreatedAt,
            AuthorName = c.Author?.UserName,
            AuthorId = c.AuthorId,
        }));
    }

    // ── POST /api/tickets/{id}/comment ────────────────
    [HttpPost("{id}/comment")]
    public async Task<IActionResult> AddComment(int id, [FromBody] AddCommentRequest req)
    {
        if (req is null || string.IsNullOrWhiteSpace(req.Text))
            return BadRequest(new { message = "Comment text is required." });

        if (!int.TryParse(User.FindFirst("sub")?.Value, out var userId))
            return Unauthorized();

        var ticket = await _db.Tickets.FindAsync(id);
        if (ticket == null) return NotFound();

        // only the submitter or the assigned agent can comment
        if (ticket.SubmittedById != userId && ticket.AssignedToId != userId)
            return Forbid();

        var author = await _db.Users.FindAsync(userId);

        var comment = new TicketComment
        {
            TicketId = id,
            Text = req.Text,
            CreatedAt = DateTime.UtcNow,
            AuthorId = userId,
        };

        _db.TicketComments.Add(comment);

        _db.ActivityLogs.Add(new ActivityLog
        {
            TicketId = id,
            UserId = userId,
            EventType = "Comment",
            Action = $"{author?.UserName} added a comment",
            Timestamp = DateTime.UtcNow,
        });

        await _db.SaveChangesAsync();

        
        return Ok(new CommentResponse
        {
            Id = comment.Id,
            Text = comment.Text,
            CreatedAt = comment.CreatedAt,
            AuthorName = author?.UserName,
            AuthorId = userId,
        });
    }
    // ── GET /api/tickets/{id}/activity ────────────────
    [HttpGet("{id}/activity")]
    public async Task<IActionResult> GetActivity(int id)
    {
        var ticket = await _db.Tickets.FindAsync(id);
        if (ticket == null) return NotFound();

        var logs = await _db.ActivityLogs
            .Include(a => a.User)
            .Where(a => a.TicketId == id)
            .OrderBy(a => a.Timestamp)
            .ToListAsync();

        return Ok(logs.Select(a => new
        {
            a.Id,
            a.Action,
            a.EventType,
            a.Timestamp,
            UserName = a.User?.UserName,
        }));
    }
    // ── POST /api/tickets/{id}/attachments ─────────────────────
    [HttpPost("{id}/attachments")]
    public async Task<IActionResult> AddAttachment(int id, [FromBody] AddAttachmentRequest req)
    {
        if (req is null || string.IsNullOrWhiteSpace(req.FileName))
            return BadRequest(new { message = "File name is required." });

        if (!int.TryParse(User.FindFirst("sub")?.Value, out var userId))
            return Unauthorized();

        var ticket = await _db.Tickets.FindAsync(id);
        if (ticket == null) return NotFound();

        var attachment = new TicketAttachment
        {
            TicketId = id,
            FileName = req.FileName,
            UploadedById = userId,
        };
        _db.TicketAttachments.Add(attachment);

        var uploader = await _db.Users.FindAsync(userId);
        _db.ActivityLogs.Add(new ActivityLog
        {
            TicketId = id,
            UserId = userId,
            EventType = "Attachment",
            Action = $"{uploader?.UserName} added attachment \"{req.FileName}\"",
            Timestamp = DateTime.UtcNow,
        });

        await _db.SaveChangesAsync();

        return Ok(new
        {
            attachment.Id,
            attachment.FileName,
            UploadedByName = uploader?.UserName,
        });
    }

    // ── GET /api/tickets/{id}/attachments ───────────────────────
    [HttpGet("{id}/attachments")]
    public async Task<IActionResult> GetAttachments(int id)
    {
        var attachments = await _db.TicketAttachments
            .Include(a => a.UploadedBy)
            .Where(a => a.TicketId == id)
            .OrderBy(a => a.Id)
            .ToListAsync();

        return Ok(attachments.Select(a => new
        {
            a.Id,
            a.FileName,
            UploadedByName = a.UploadedBy?.UserName,
        }));
    }

    public record AddAttachmentRequest(string FileName);


    public record AssignRequest(int UserId);

}