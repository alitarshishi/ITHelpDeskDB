// Services/NotificationService.cs
using ITHelpDeskDb.Data;
using ITHelpDeskDb.Hubs;
using ITHelpDeskDb.Models;
using Microsoft.AspNetCore.SignalR;

namespace ITHelpDeskDb.Services;

public class NotificationService
{
    private readonly AppDbContext _db;
    private readonly IHubContext<NotificationHub> _hub;

    public NotificationService(AppDbContext db, IHubContext<NotificationHub> hub)
    {
        _db = db;
        _hub = hub;
    }

    public async Task NotifyAsync(int recipientId, int? ticketId, string trigger, string message)
    {
        var notification = new Notification
        {
            RecipientId = recipientId,
            TicketId = ticketId,
            Trigger = trigger,
            Message = message,
            CreatedAt = DateTime.UtcNow,
            IsRead = false,
        };

        _db.Notifications.Add(notification);
        await _db.SaveChangesAsync(); 

        
        await _hub.Clients.Group($"user-{recipientId}").SendAsync("ReceiveNotification", new
        {
            notification.Id,
            notification.Message,
            notification.CreatedAt,
            notification.IsRead,
            notification.TicketId,
            notification.Trigger,
        });
    }
}
