using ITHelpDeskDb.Data;
using Microsoft.EntityFrameworkCore;

namespace ITHelpDeskDb.Services;

public class NotificationQueryService
{
    private readonly AppDbContext _db;
    public NotificationQueryService(AppDbContext db) => _db = db;

    public async Task<List<object>> GetMineAsync(int userId)
    {
        var result = await _db.Notifications
            .Where(n => n.RecipientId == userId)
            .OrderByDescending(n => n.CreatedAt)
            .Take(50)
            .Select(n => (object)new
            {
                n.Id,
                n.Message,
                n.CreatedAt,
                n.IsRead,
                n.TicketId,
                n.Trigger,
            })
            .ToListAsync();

        return result;
    }

    public async Task<int> GetUnreadCountAsync(int userId) =>
        await _db.Notifications.CountAsync(n => n.RecipientId == userId && !n.IsRead);

    public async Task<bool> MarkReadAsync(int notificationId, int userId)
    {
        var notif = await _db.Notifications
            .FirstOrDefaultAsync(n => n.Id == notificationId && n.RecipientId == userId);
        if (notif == null) return false;

        notif.IsRead = true;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task MarkAllReadAsync(int userId)
    {
        // ExecuteUpdateAsync translates to a single UPDATE WHERE statement in SQL
        // instead of loading all rows into memory, updating each, then saving.
        await _db.Notifications
            .Where(n => n.RecipientId == userId && !n.IsRead)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true));
    }

    public async Task ClearAllAsync(int userId)
    {
        // ExecuteDeleteAsync translates to a single DELETE WHERE statement —
        // never loads the rows into memory at all.
        await _db.Notifications
            .Where(n => n.RecipientId == userId)
            .ExecuteDeleteAsync();
    }
}
