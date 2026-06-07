using ITHelpDeskDb.Data;
using ITHelpDeskDb.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

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
            .Include(t => t.Priority)
            .Include(t => t.Category)
            .Include(t => t.Status)
            .OrderByDescending(t => t.DateCreated)
            .ToListAsync();
        var dto = tickets.Select(t => new
        {
            t.Id,
            t.Title,
            t.Description,
            t.DateCreated,
            StatusName = t.Status?.Name,
            PriorityName = t.Priority?.Name,
            CategoryName = t.Category?.Name,
            AssignedToName = t.AssignedTo?.UserName,
            SubmittedByName = t.SubmittedBy?.UserName,
        });

        return Ok(dto);
    }
   

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(int id)
    {
        var t = await _db.Tickets
            .Include(t => t.SubmittedBy)
            .Include(t => t.AssignedTo)
            .Include(t => t.Priority)
            .Include(t => t.Category)
            .Include(t => t.Status)
            .FirstOrDefaultAsync(t => t.Id == id);
        if (t == null) return NotFound();
        return Ok(new
        {
            t.Id,
            t.Title,
            t.Description,
            t.DateCreated,
            StatusName = t.Status?.Name,
            PriorityName = t.Priority?.Name,
            CategoryName = t.Category?.Name,
            AssignedToName = t.AssignedTo?.UserName,
            SubmittedByName = t.SubmittedBy?.UserName,
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
            .Where(t => t.SubmittedById == userId)
            .OrderByDescending(t => t.DateCreated)
            .ToListAsync();

        return Ok(tickets.Select(t => new
        {
            t.Id,
            t.Title,
            t.Description,
            t.DateCreated,
            StatusName = t.Status?.Name,
            PriorityName = t.Priority?.Name,
            CategoryName = t.Category?.Name,
            AssignedToName = t.AssignedTo?.UserName,
        }));
    }
}


public record AssignRequest(int UserId);

public record CreateTicketRequest(
    string Title,
    string Description,
    int CategoryId,
    int PriorityId,
    int StatusId,
    int SubmittedById,
    int? AssignedToId
);
