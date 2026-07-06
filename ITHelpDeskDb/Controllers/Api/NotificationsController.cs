using ITHelpDeskDb.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ITHelpDeskDb.Controllers.Api;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class NotificationsController : ControllerBase
{
    private readonly NotificationQueryService _notifications;
    public NotificationsController(NotificationQueryService notifications)
        => _notifications = notifications;

    [HttpGet]
    public async Task<IActionResult> GetMine()
    {
        var userId = int.Parse(User.FindFirst("sub")!.Value);
        return Ok(await _notifications.GetMineAsync(userId));
    }

    [HttpGet("unread-count")]
    public async Task<IActionResult> UnreadCount()
    {
        var userId = int.Parse(User.FindFirst("sub")!.Value);
        return Ok(new { count = await _notifications.GetUnreadCountAsync(userId) });
    }

    [HttpPut("{id}/read")]
    public async Task<IActionResult> MarkRead(int id)
    {
        var userId = int.Parse(User.FindFirst("sub")!.Value);
        var found = await _notifications.MarkReadAsync(id, userId);
        return found ? NoContent() : NotFound();
    }

    [HttpPut("read-all")]
    public async Task<IActionResult> MarkAllRead()
    {
        var userId = int.Parse(User.FindFirst("sub")!.Value);
        await _notifications.MarkAllReadAsync(userId);
        return NoContent();
    }

    [HttpDelete("clear-all")]
    public async Task<IActionResult> ClearAll()
    {
        var userId = int.Parse(User.FindFirst("sub")!.Value);
        await _notifications.ClearAllAsync(userId);
        return NoContent();
    }
}