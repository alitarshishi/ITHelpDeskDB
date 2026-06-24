using ClosedXML.Excel;
using ITHelpDeskDb.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ITHelpDeskDb.Controllers.Api;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin")]
public class ExportController : ControllerBase
{
    private readonly AppDbContext _db;
    public ExportController(AppDbContext db) => _db = db;

    // ── GET /api/export/tickets.xlsx?period=week|2weeks|month|all ──
    [HttpGet("tickets.xlsx")]
    public async Task<IActionResult> ExportTicketsExcel([FromQuery] string period = "all")
    {
        var since = period switch
        {
            "week" => DateTime.UtcNow.AddDays(-7),
            "2weeks" => DateTime.UtcNow.AddDays(-14),
            "month" => DateTime.UtcNow.AddMonths(-1),
            _ => (DateTime?)null, // "all"
        };

        var query = _db.Tickets
            .Include(t => t.Status)
            .Include(t => t.Priority)
            .Include(t => t.Category)
            .Include(t => t.SubmittedBy)
            .Include(t => t.AssignedTo)
            .Include(t => t.AssignedByManager)
            .AsQueryable();

        if (since != null)
            query = query.Where(t => t.DateCreated >= since);

        var tickets = await query.OrderByDescending(t => t.DateCreated).ToListAsync();

        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Tickets");

        // Header row
        var headers = new[]
        {
            "Ticket ID", "Title", "Description", "Status", "Priority",
            "Category", "Created By", "Assigned By", "Assigned To",
            "Created", "Resolved"
        };
        for (int i = 0; i < headers.Length; i++)
        {
            sheet.Cell(1, i + 1).Value = headers[i];
            sheet.Cell(1, i + 1).Style.Font.Bold = true;
            sheet.Cell(1, i + 1).Style.Fill.BackgroundColor = XLColor.FromHtml("#111111");
            sheet.Cell(1, i + 1).Style.Font.FontColor = XLColor.White;
        }

        // Data rows
        int row = 2;
        foreach (var t in tickets)
        {
            sheet.Cell(row, 1).Value = $"TKT-{t.Id:D4}";
            sheet.Cell(row, 2).Value = t.Title;
            sheet.Cell(row, 3).Value = t.Description;
            sheet.Cell(row, 4).Value = t.Status?.Name;
            sheet.Cell(row, 5).Value = t.Priority?.Name;
            sheet.Cell(row, 6).Value = t.Category?.Name;
            sheet.Cell(row, 7).Value = t.SubmittedBy?.UserName;
            sheet.Cell(row, 8).Value = t.AssignedByManager?.UserName ?? "—";
            sheet.Cell(row, 9).Value = t.AssignedTo?.UserName ?? "Unassigned";
            sheet.Cell(row, 10).Value = t.DateCreated.ToString("yyyy-MM-dd HH:mm");
            sheet.Cell(row, 11).Value = t.DateResolved?.ToString("yyyy-MM-dd HH:mm") ?? "—";
            row++;
        }

        sheet.Columns().AdjustToContents();
        sheet.SheetView.FreezeRows(1);

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        ms.Position = 0;

        var fileName = $"tickets-export-{DateTime.UtcNow:yyyyMMdd-HHmm}.xlsx";
        return File(ms.ToArray(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            fileName);
    }
}