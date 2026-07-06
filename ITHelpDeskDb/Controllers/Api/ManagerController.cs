using ITHelpDeskDb.Models.DTOs.Requests;
using ITHelpDeskDb.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ITHelpDeskDb.Controllers.Api;

[ApiController]
[Route("api/manager")]
[Authorize(Roles = "Manager")]
public class ManagerController : ControllerBase
{
    private readonly TicketService _tickets;
    private readonly UserService _users;

    public ManagerController(TicketService tickets, UserService users)
    {
        _tickets = tickets;
        _users = users;
    }

    [HttpGet("team-tickets")]
    public async Task<IActionResult> TeamTickets()
    {
        var managerId = int.Parse(User.FindFirst("sub")!.Value);
        return Ok(await _tickets.GetTeamTicketsAsync(managerId));
    }

    [HttpPatch("{id}/update")]
    public async Task<IActionResult> UpdateTicket(int id, [FromBody] ManagerUpdateRequest req)
    {
        var managerId = int.Parse(User.FindFirst("sub")!.Value);
        var managerName = await _users.GetUserNameAsync(managerId) ?? "Manager";

        var (found, error) = await _tickets.ManagerUpdateAsync(id, req, managerId, managerName);
        if (!found) return NotFound();
        if (error != null) return BadRequest(new { message = error });
        return NoContent();
    }

    [HttpGet("reports/status-counts")]
    public async Task<IActionResult> StatusCounts()
    {
        var managerId = int.Parse(User.FindFirst("sub")!.Value);
        return Ok(await _tickets.GetTeamTicketsAsync(managerId));
    }
}

