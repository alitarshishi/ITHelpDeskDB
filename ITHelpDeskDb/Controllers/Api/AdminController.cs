using ITHelpDeskDb.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ITHelpDeskDb.Controllers.Api;

[ApiController]
[Route("api/admin")]
[Authorize(Roles = "Admin")]
public class AdminController : ControllerBase
{
    private readonly AppDbContext _db;

    public AdminController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet("dashboard")]
    public async Task<IActionResult> Dashboard()
    {
        var totalUsers = await _db.Users.CountAsync();
        var totalTickets = await _db.Tickets.CountAsync();
        var openTickets = await _db.Tickets.Where(t => t.StatusId == 1).CountAsync();

        return Ok(new
        {
            totalUsers,
            totalTickets,
            openTickets
        });
    }
}
