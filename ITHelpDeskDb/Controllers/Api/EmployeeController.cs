using ITHelpDeskDb.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ITHelpDeskDb.Controllers.Api;

[ApiController]
[Route("api/employee")]
[Authorize(Roles = "Employee")]
public class EmployeeController : ControllerBase
{
    private readonly AppDbContext _db;

    public EmployeeController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet("dashboard")]
    public async Task<IActionResult> Dashboard()
    {
        var myId = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
        var myAssigned = await _db.Tickets.Where(t => t.AssignedToId == myId).ToListAsync();
        var mySubmitted = await _db.Tickets.Where(t => t.SubmittedById == myId).ToListAsync();

        return Ok(new
        {
            assignedCount = myAssigned.Count,
            submittedCount = mySubmitted.Count,
            assigned = myAssigned,
            submitted = mySubmitted
        });
    }
}
