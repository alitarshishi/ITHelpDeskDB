using ITHelpDeskDb.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ITHelpDeskDb.Controllers.Api;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin,Manager")]
public class ExportController : ControllerBase
{
    private readonly ExportService _export;
    public ExportController(ExportService export) => _export = export;

    [HttpGet("tickets.xlsx")]
    public async Task<IActionResult> ExportTicketsExcel([FromQuery] string period = "all")
    {
        var (bytes, fileName) = await _export.ExportTicketsExcelAsync(period);
        return File(bytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            fileName);
    }
}