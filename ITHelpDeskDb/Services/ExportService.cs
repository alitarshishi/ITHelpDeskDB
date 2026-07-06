using ClosedXML.Excel;
using ITHelpDeskDb.Data;
using Microsoft.EntityFrameworkCore;

namespace ITHelpDeskDb.Services;

public class ExportService
{
    private readonly AppDbContext _db;
    public ExportService(AppDbContext db) => _db = db;

    public async Task<(byte[] bytes, string fileName)> ExportTicketsExcelAsync(string period)
    {
        var since = period switch
        {
            "week" => (DateTime?)DateTime.UtcNow.AddDays(-7),
            "2weeks" => DateTime.UtcNow.AddDays(-14),
            "month" => DateTime.UtcNow.AddMonths(-1),
            _ => null,
        };

        var query = _db.Tickets.AsQueryable();
        if (since != null)
            query = query.Where(t => t.DateCreated >= since);

        // ── Tickets projection ───────────────────────────────────────────────
        var tickets = await query
            .OrderByDescending(t => t.DateCreated)
            .Select(t => new
            {
                t.Id,
                TicketId = $"TKT-{t.Id:D4}",
                t.Title,
                t.Description,
                Status = t.Status.Name,
                Priority = t.Priority.Name,
                Category = t.Category.Name,
                CreatedBy = t.SubmittedBy.UserName,
                AssignedBy = t.AssignedByManager != null ? t.AssignedByManager.UserName : "—",
                AssignedTo = t.AssignedTo != null ? t.AssignedTo.UserName : "Unassigned",
                Created = t.DateCreated.ToString("yyyy-MM-dd HH:mm"),
                Resolved = t.DateResolved != null
                               ? t.DateResolved.Value.ToString("yyyy-MM-dd HH:mm")
                               : "—",
            })
            .ToListAsync();

        // ── Activity logs for those tickets ─────────────────────────────────
        var ticketIds = tickets.Select(t => t.Id).ToList();

        var activityLogs = await _db.ActivityLogs
            .Where(a => ticketIds.Contains((int)a.TicketId))
            .OrderBy(a => a.TicketId)
            .ThenBy(a => a.Timestamp)
            .Select(a => new
            {
                TicketId = $"TKT-{a.TicketId:D4}",
                a.EventType,
                a.Action,
                Timestamp = a.Timestamp.ToString("yyyy-MM-dd HH:mm"),
                UserName = a.User != null ? a.User.UserName : "—",
            })
            .ToListAsync();

        using var workbook = new XLWorkbook();

        // ════════════════════════════════════════════════════════════════════
        // Sheet 1 — Tickets
        // ════════════════════════════════════════════════════════════════════
        var ticketSheet = workbook.Worksheets.Add("Tickets");

        var ticketHeaders = new[]
        {
            "Ticket ID", "Title", "Description", "Status", "Priority",
            "Category", "Created By", "Assigned By", "Assigned To",
            "Created", "Resolved",
        };

        StyleHeaderRow(ticketSheet, ticketHeaders);

        int row = 2;
        foreach (var t in tickets)
        {
            ticketSheet.Cell(row, 1).Value = t.TicketId;
            ticketSheet.Cell(row, 2).Value = t.Title;
            ticketSheet.Cell(row, 3).Value = t.Description;
            ticketSheet.Cell(row, 4).Value = t.Status;
            ticketSheet.Cell(row, 5).Value = t.Priority;
            ticketSheet.Cell(row, 6).Value = t.Category;
            ticketSheet.Cell(row, 7).Value = t.CreatedBy;
            ticketSheet.Cell(row, 8).Value = t.AssignedBy;
            ticketSheet.Cell(row, 9).Value = t.AssignedTo;
            ticketSheet.Cell(row, 10).Value = t.Created;
            ticketSheet.Cell(row, 11).Value = t.Resolved;

            // Alternate row shading for readability
            if (row % 2 == 0)
                ticketSheet.Row(row).Style.Fill.BackgroundColor = XLColor.FromHtml("#f9fafb");

            row++;
        }

        ticketSheet.Columns().AdjustToContents();
        ticketSheet.SheetView.FreezeRows(1);

        // ════════════════════════════════════════════════════════════════════
        // Sheet 2 — Activity Logs
        // ════════════════════════════════════════════════════════════════════
        var logSheet = workbook.Worksheets.Add("Activity Logs");

        var logHeaders = new[]
        {
            "Ticket ID", "Event Type", "Action", "Performed By", "Timestamp",
        };

        StyleHeaderRow(logSheet, logHeaders);

        int logRow = 2;
        foreach (var log in activityLogs)
        {
            logSheet.Cell(logRow, 1).Value = log.TicketId;
            logSheet.Cell(logRow, 2).Value = log.EventType;
            logSheet.Cell(logRow, 3).Value = log.Action;
            logSheet.Cell(logRow, 4).Value = log.UserName;
            logSheet.Cell(logRow, 5).Value = log.Timestamp;

            if (logRow % 2 == 0)
                logSheet.Row(logRow).Style.Fill.BackgroundColor = XLColor.FromHtml("#f9fafb");

            logRow++;
        }

        logSheet.Columns().AdjustToContents();
        logSheet.SheetView.FreezeRows(1);

        // ── Save ────────────────────────────────────────────────────────────
        using var ms = new MemoryStream();
        workbook.SaveAs(ms);

        var fileName = $"tickets-export-{DateTime.UtcNow:yyyyMMdd-HHmm}.xlsx";
        return (ms.ToArray(), fileName);
    }

    // ── Shared header row styling ────────────────────────────────────────────
    private static void StyleHeaderRow(IXLWorksheet sheet, string[] headers)
    {
        for (int i = 0; i < headers.Length; i++)
        {
            var cell = sheet.Cell(1, i + 1);
            cell.Value = headers[i];
            cell.Style.Font.Bold = true;
            cell.Style.Font.FontColor = XLColor.White;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#111111");
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        }
    }
}