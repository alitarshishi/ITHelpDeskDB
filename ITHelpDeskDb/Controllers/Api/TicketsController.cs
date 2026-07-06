using ITHelpDeskDb.Models.DTOs.Requests;
using ITHelpDeskDb.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace ITHelpDeskDb.Controllers.Api;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class TicketsController : ControllerBase
{
    private readonly TicketService _tickets;
    private readonly UserService _users;

    public TicketsController(TicketService tickets, UserService users)
    {
        _tickets = tickets;
        _users = users;
    }

[HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetAll() =>
        Ok(await _tickets.GetAllAsync());

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(int id)
    {
        var ticket = await _tickets.GetByIdAsync(id);
        return ticket == null ? NotFound() : Ok(ticket);
    }

    [HttpGet("my")]
    public async Task<IActionResult> GetMine()
    {
        var userId = int.Parse(User.FindFirst("sub")!.Value);
        return Ok(await _tickets.GetBySubmitterAsync(userId));
    }



    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateTicketRequest req)
    {
        if (req == null) return BadRequest();
        var submitterName = await _users.GetUserNameAsync(req.SubmittedById) ?? "Unknown";
        var id = await _tickets.CreateAsync(req, submitterName);
        return CreatedAtAction(nameof(Get), new { id }, new { id });
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateTicketRequest req)
    {
        var updated = await _tickets.UpdateAsync(id, req);
        return updated ? NoContent() : NotFound();
    }


    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var userId = int.Parse(User.FindFirst("sub")!.Value);
        var role = User.FindFirst("role")?.Value ?? "";

        var (found, allowed, message) = await _tickets.DeleteAsync(id, userId, role);
        if (!found) return NotFound();
        if (!allowed) return message == "Forbidden" ? Forbid() : BadRequest(new { message });
        return NoContent();
    }

    // ── GET /api/tickets/{id}/comments ────────────────
    [HttpGet("{id}/comments")]
    public async Task<IActionResult> GetComments(int id)
    {
        if (!await _tickets.TicketExistsAsync(id)) return NotFound();
        return Ok(await _tickets.GetCommentsAsync(id));
    }

    // ── POST /api/tickets/{id}/comment ────────────────
    [HttpPost("{id}/comment")]
    public async Task<IActionResult> AddComment(int id, [FromBody] AddCommentRequest req)
    {
        if (req is null || string.IsNullOrWhiteSpace(req.Text))
            return BadRequest(new { message = "Comment text is required." });

        if (!int.TryParse(User.FindFirst("sub")?.Value, out var userId))
            return Unauthorized();

        var role = User.FindFirst("role")?.Value ?? "";
        var (comment, error) = await _tickets.AddCommentAsync(id, req.Text, userId, role);

        return error switch
        {
            "NotFound" => NotFound(),
            "Forbidden" => Forbid(),
            null => Ok(comment),
            _ => BadRequest(new { message = error }),
        };
    }
    // ── GET /api/tickets/{id}/activity ────────────────
    [HttpGet("{id}/attachments")]
    public async Task<IActionResult> GetAttachments(int id) =>
        Ok(await _tickets.GetAttachmentsAsync(id));
    // ── POST /api/tickets/{id}/attachments ─────────────────────
    [HttpPost("{id}/attachments")]
    public async Task<IActionResult> AddAttachment(int id, [FromForm] IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { message = "No file provided." });

        if (!int.TryParse(User.FindFirst("sub")?.Value, out var userId))
            return Unauthorized();

        var (result, error) = await _tickets.AddAttachmentAsync(id, file, userId);

        return error switch
        {
            "NotFound" => NotFound(),
            null => Ok(result),
            _ => BadRequest(new { message = error }),
        };
    }



    // ── GET /api/tickets/attachments/{attachmentId}/view — opens the file ──
    [HttpGet("attachments/{attachmentId}/view")]
    public async Task<IActionResult> ViewAttachment(int attachmentId)
    {
        var (content, contentType) = await _tickets.GetAttachmentBytesAsync(attachmentId);
        if (content == null) return NotFound();
        return File(content, contentType ?? "application/octet-stream");
    }

    [HttpGet("{id}/activity")]
    public async Task<IActionResult> GetActivity(int id) =>
        Ok(await _tickets.GetActivityAsync(id));
}




