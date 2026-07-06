using ITHelpDeskDb.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ITHelpDeskDb.Controllers.Api;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin,Manager")]
public class DashboardController : ControllerBase
{
    private readonly DashboardService _dashboard;
    public DashboardController(DashboardService dashboard) => _dashboard = dashboard;

    [HttpGet("stats")]
    public async Task<IActionResult> GetStats([FromQuery] string period = "week")
    {
        var role = User.FindFirst("role")?.Value ?? "";
        var userId = int.Parse(User.FindFirst("sub")!.Value);
        var result = await _dashboard.GetStatsAsync(period, role, userId);
        return Ok(result);
    }
}